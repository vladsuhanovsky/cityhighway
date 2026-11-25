using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Клубы и друзья».
    /// - **Мой клуб**: логотип, название и тег, код приглашения, цель недели с полосой прогресса и вкладом каждого
    ///   участника, заработанные логотипы (владелец выбирает герб), рекорды участников с повторами.
    /// - **Найти клуб**: открытые клубы, поиск, вступление по коду; **создать клуб** — название, тег, стартовый логотип.
    /// - **Рекорды друзей**: лучшие результаты друзей Steam на каждой карте — повтор и «Бросить вызов» (рейд).
    /// Всё облачное (Steam + сервер). Без Steam — объяснение и рекорды других профилей этого компьютера.
    /// </summary>
    public sealed class MenuClubPage
    {
        static bool En => LocalizationService.Language == "en";
        enum Tab { Club, Friends }

        readonly CityHighwaysApp _app;
        readonly VisualElement _body;
        readonly Label _status;
        readonly Label _pendingReports;
        Tab _tab = Tab.Club;
        bool _searching;
        int _searchRevision;
        string _query = "";
        SocialCloud.ClubBrief[] _found;
        string _viewSteamAccount;
        int _viewSteamGeneration = -1;
        string _newLogo = ClubLogo.Starters[0];
        string _newName = "", _newTag = "", _code = "";
        bool _newOpen = true;
        readonly List<Button> _tabButtons = new List<Button>();

        public VisualElement Root { get; }

        public MenuClubPage(CityHighwaysApp app, Action back)
        {
            _app = app;
            Root = Ui.Column();

            var header = Ui.Row();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 14f;
            var backButton = Ui.Button(LocalizationService.Get("menu.clubs.001"), back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            Ui.MarkBack(Root, back);
            header.Add(backButton);
            var title = Ui.Text(LocalizationService.Get("menu.clubs.002"), 34f, Theme.Gold, true);
            title.style.flexGrow = 1f;
            header.Add(title);
            var refresh = Ui.Button(LocalizationService.Get("menu.clubs.003"), () =>
            {
                SocialCloud.RefreshClub();
                SocialCloud.RefreshFriends();
                SetStatus(LocalizationService.Get("menu.clubs.004"), Theme.TextDim);
            }, ButtonKind.Secondary, 13f);
            header.Add(refresh);
            Root.Add(header);

            var tabs = Ui.Row();
            tabs.style.marginBottom = 12f;
            AddTab(tabs, LocalizationService.Get("menu.clubs.005"), Tab.Club);
            AddTab(tabs, LocalizationService.Get("menu.clubs.006"), Tab.Friends);
            Root.Add(tabs);

            _status = Ui.Text("", 13f, Theme.TextDim);
            _status.style.marginBottom = 8f;
            _status.style.whiteSpace = WhiteSpace.Normal;
            Root.Add(_status);

            _pendingReports = Ui.Text("", 13f, Theme.Gold);
            _pendingReports.style.marginBottom = 8f;
            _pendingReports.style.whiteSpace = WhiteSpace.Normal;
            Root.Add(_pendingReports);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 760f;
            Ui.StyleScroller(scroll);
            _body = Ui.Column();
            scroll.Add(_body);
            Root.Add(scroll);

            // Облако обновилось — перерисовать, пока страница на экране (подписка снимается, когда меню закрыто).
            Action onChanged = null;
            onChanged = () =>
            {
                if (Root.panel == null)
                {
                    SocialCloud.Changed -= onChanged;
                    return;
                }
                Rebuild();
            };
            SocialCloud.Changed += onChanged;
            Rebuild();
        }

        void AddTab(VisualElement row, string caption, Tab tab)
        {
            var button = Ui.Button(caption, () =>
            {
                _tab = tab;
                Rebuild();
            }, ButtonKind.Secondary, 15f);
            button.style.marginRight = 8f;
            button.style.minWidth = 170f;
            button.userData = tab;
            _tabButtons.Add(button);
            row.Add(button);
        }

        /// <summary>В строке состояния — временная подсказка («Загружаем…»), которую можно стереть при перерисовке.</summary>
        bool _statusTransient;

        void SetStatus(string text, Color color)
        {
            _status.text = text ?? "";
            _status.style.color = color;
            _statusTransient = color == Theme.TextDim;
        }

        void Fail(string error) => SetStatus(error, Theme.Danger);

        public void Rebuild()
        {
            string owner = SocialCloud.CurrentSteamAccount;
            int generation = SocialCloud.CurrentAccountGeneration;
            if (_viewSteamGeneration != generation || _viewSteamAccount != owner)
            {
                // Search results and invite codes are private to the session
                // that fetched them. A -> B -> A must reset these too.
                _viewSteamAccount = owner;
                _viewSteamGeneration = generation;
                unchecked { _searchRevision++; }
                _found = null;
                _searching = false;
                _query = "";
                _code = "";
                _newName = "";
                _newTag = "";
            }
            int pending = SocialCloud.PendingReportsForCurrentAccount;
            _pendingReports.text = pending > 0
                ? (En ? $"Pending game reports: {pending}. They will retry when Steam is connected."
                      : $"Ожидают отправки отчёты партий: {pending}. Повторная отправка — при подключении к Steam.")
                : "";
            Ui.Show(_pendingReports, pending > 0);
            foreach (var button in _tabButtons)
            {
                bool active = (Tab)button.userData == _tab;
                button.style.borderBottomWidth = 3f;
                button.style.borderBottomColor = active ? Theme.Gold : Color.clear;
            }
            _body.Clear();
            if (!SocialCloud.Available)
            {
                var card = Card();
                card.Add(Ui.Text(SocialCloud.Unavailable, 16f, Theme.Text, true));
                var hint = Ui.Text(LocalizationService.Get("menu.clubs.007") +
                                   LocalizationService.Get("menu.clubs.008"), 13f, Theme.TextDim);
                hint.style.whiteSpace = WhiteSpace.Normal;
                hint.style.marginTop = 6f;
                card.Add(hint);
                _body.Add(card);
                if (_tab == Tab.Friends) BuildLocalRecords();
                return;
            }
            if (_tab == Tab.Club) BuildClub();
            else BuildFriends();
        }

        static VisualElement Card()
        {
            var card = Ui.Card(18f);
            card.style.backgroundColor = Theme.Panel;
            card.style.marginBottom = 12f;
            Ui.Radius(card, 12f);
            return card;
        }

        // ================================================================== Мой клуб

        void BuildClub()
        {
            var state = SocialCloud.Club;
            if (state == null)
            {
                // Клуб подтягивается сам (SocialCloud.Tick); здесь только показываем, что идёт загрузка или что мешает.
                if (string.IsNullOrEmpty(SocialCloud.Problem)) SetStatus(LocalizationService.Get("menu.clubs.009"), Theme.TextDim);
                else Fail(LocalizationService.Get("menu.clubs.010") + SocialCloud.Problem + LocalizationService.Get("menu.clubs.011"));
                return;
            }
            if (!state.HasClub)
            {
                BuildNoClub();
                return;
            }
            if (_statusTransient) SetStatus("", Theme.TextDim);
            var club = state.club;

            // Шапка клуба.
            var head = Card();
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            var logo = new ClubLogo(club.logo, 84f);
            logo.style.marginRight = 18f;
            row.Add(logo);
            var titles = Ui.Column();
            titles.style.flexGrow = 1f;
            titles.Add(Ui.Text($"{club.name}  [{club.tag}]", 26f, Theme.Gold, true));
            titles.Add(Ui.Text((En ? $"Members: {state.members?.Length ?? 0} of 30 · weekly goals completed: {state.weeksDone}" : $"Участников: {state.members?.Length ?? 0} из 30 · целей недели выполнено: {state.weeksDone}") +
                               (club.open ? LocalizationService.Get("menu.clubs.012") : LocalizationService.Get("menu.clubs.013")), 13f, Theme.TextDim));
            var code = Ui.Text((En ? $"Invite code: {club.code}" : $"Код приглашения: {club.code}"), 15f, Theme.Text, true);
            code.style.marginTop = 6f;
            titles.Add(code);
            row.Add(titles);
            var buttons = Ui.Column();
            var copy = Ui.Button(LocalizationService.Get("menu.clubs.014"), () =>
            {
                GUIUtility.systemCopyBuffer = club.code;
                SetStatus(LocalizationService.Get("menu.clubs.015"), Theme.Success);
            }, ButtonKind.Secondary, 13f);
            copy.style.marginBottom = 6f;
            buttons.Add(copy);
            if (club.owner)
            {
                var open = Ui.Button(club.open ? LocalizationService.Get("menu.clubs.016") : LocalizationService.Get("menu.clubs.017"), () => SocialCloud.SetClubOpen(!club.open, Fail), ButtonKind.Ghost, 13f);
                open.style.marginBottom = 6f;
                buttons.Add(open);
            }
            buttons.Add(Ui.Button(LocalizationService.Get("menu.clubs.018"), () => SocialCloud.LeaveClub(Fail), ButtonKind.Ghost, 13f));
            row.Add(buttons);
            head.Add(row);
            _body.Add(head);

            // Цель недели.
            var week = state.week;
            if (week != null && !string.IsNullOrEmpty(week.kind))
            {
                var goal = Card();
                var goalHead = Ui.Row();
                goalHead.style.alignItems = Align.Center;
                var flag = new MenuIcon(MenuIcon.Glyph.Flag, 22f, week.completed ? Theme.Success : Theme.Gold);
                flag.style.marginRight = 10f;
                goalHead.Add(flag);
                var goalTitle = Ui.Text(LocalizationService.Get("menu.clubs.019") + SocialCloud.GoalTitle(week.kind, week.target), 18f, Theme.Text, true);
                goalTitle.style.flexGrow = 1f;
                goalHead.Add(goalTitle);
                goalHead.Add(Ui.Text(EndsIn(week.endsAt), 13f, Theme.TextDim));
                goal.Add(goalHead);
                var hint = Ui.Text(SocialCloud.GoalHint(week.kind) + LocalizationService.Get("menu.clubs.020"),
                    12f, Theme.TextDim);
                hint.style.whiteSpace = WhiteSpace.Normal;
                hint.style.marginTop = 4f;
                goal.Add(hint);
                goal.Add(ProgressBar(week.progress, week.target, week.completed ? Theme.Success : Theme.Gold));
                var numbers = Ui.Text(week.completed
                    ? (En ? $"Completed: {week.progress} of {week.target} {SocialCloud.GoalUnit(week.kind, week.target)} — logo unlocked!" : $"Выполнено: {week.progress} из {week.target} {SocialCloud.GoalUnit(week.kind, week.target)} — логотип получен!")
                    : $"{week.progress} из {week.target} {SocialCloud.GoalUnit(week.kind, week.target)}", 14f, week.completed ? Theme.Success : Theme.Text, true);
                numbers.style.marginTop = 4f;
                goal.Add(numbers);

                // Вклад участников.
                var members = (state.members ?? new SocialCloud.MemberInfo[0]).OrderByDescending(m => m.amount).ToList();
                int top = Math.Max(1, members.Count > 0 ? members.Max(m => m.amount) : 1);
                var list = Ui.Column();
                list.style.marginTop = 12f;
                foreach (var member in members)
                {
                    var line = Ui.Row();
                    line.style.alignItems = Align.Center;
                    line.style.marginBottom = 4f;
                    var name = Ui.Text(member.name + (member.role == "owner" ? " ♛" : "") + (member.me ? LocalizationService.Get("menu.clubs.021") : ""), 14f, member.me ? Theme.Gold : Theme.Text, member.me);
                    name.style.width = 220f;
                    line.Add(name);
                    var bar = Ui.Box();
                    bar.style.flexGrow = 1f;
                    bar.style.height = 10f;
                    bar.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
                    Ui.Radius(bar, 5f);
                    var fill = Ui.Box();
                    fill.style.height = 10f;
                    fill.style.width = Length.Percent(100f * member.amount / top);
                    fill.style.backgroundColor = member.me ? Theme.Gold : new Color(0.55f, 0.70f, 0.95f);
                    Ui.Radius(fill, 5f);
                    bar.Add(fill);
                    line.Add(bar);
                    var amount = Ui.Number((En ? $"{member.amount} · games {member.games}" : $"{member.amount} · партий {member.games}"), 12f, Theme.TextDim);
                    amount.style.width = 140f;
                    amount.style.unityTextAlign = TextAnchor.MiddleRight;
                    line.Add(amount);
                    list.Add(line);
                }
                goal.Add(list);
                _body.Add(goal);
            }

            // Логотипы клуба.
            var logos = Card();
            logos.Add(Ui.SectionTitle(LocalizationService.Get("menu.clubs.022")));
            var logoRow = Ui.Row();
            logoRow.style.flexWrap = Wrap.Wrap;
            foreach (var item in state.logos ?? new SocialCloud.LogoInfo[0])
            {
                var cell = Ui.Column();
                cell.style.alignItems = Align.Center;
                cell.style.width = 110f;
                cell.style.marginRight = 8f;
                cell.style.marginBottom = 8f;
                Ui.Padding(cell, 8f, 6f);
                Ui.Radius(cell, 10f);
                bool active = item.logo == club.logo;
                cell.style.backgroundColor = active ? new Color(1f, 0.8f, 0.3f, 0.14f) : Theme.PanelLight;
                cell.Add(new ClubLogo(item.logo, 56f));
                var caption = Ui.Text(string.IsNullOrEmpty(item.week) ? LocalizationService.Get("menu.clubs.023") : $"{ClubLogo.TierTitle(item.logo)} · {item.week}", 11f, Theme.TextDim);
                caption.style.marginTop = 4f;
                caption.style.unityTextAlign = TextAnchor.MiddleCenter;
                caption.style.whiteSpace = WhiteSpace.Normal;
                cell.Add(caption);
                if (active) cell.Add(Ui.Text(LocalizationService.Get("menu.clubs.024"), 11f, Theme.Gold, true));
                else if (club.owner)
                {
                    string logoId = item.logo;
                    var use = Ui.Button(LocalizationService.Get("menu.clubs.025"), () => SocialCloud.SetClubLogo(logoId, Fail), ButtonKind.Ghost, 11f);
                    use.style.marginTop = 2f;
                    cell.Add(use);
                }
                logoRow.Add(cell);
            }
            logos.Add(logoRow);
            var logoHint = Ui.Text(LocalizationService.Get("menu.clubs.026") +
                                   LocalizationService.Get("menu.clubs.027"), 12f, Theme.TextDim);
            logoHint.style.whiteSpace = WhiteSpace.Normal;
            logos.Add(logoHint);
            _body.Add(logos);

            // Рекорды участников — повторы партий клубных товарищей.
            if (state.records != null && state.records.Length > 0)
            {
                var records = Card();
                records.Add(Ui.SectionTitle(LocalizationService.Get("menu.clubs.028")));
                foreach (var record in state.records.Take(15))
                    records.Add(RecordRow(record, record.name == state.members?.FirstOrDefault(m => m.me)?.name ? "self" : "club"));
                _body.Add(records);
            }
        }

        void BuildNoClub()
        {
            if (_statusTransient) SetStatus("", Theme.TextDim);
            var intro = Card();
            intro.Add(Ui.Text(LocalizationService.Get("menu.clubs.029"), 20f, Theme.Text, true));
            var hint = Ui.Text(LocalizationService.Get("menu.clubs.030") +
                               LocalizationService.Get("menu.clubs.031") +
                               LocalizationService.Get("menu.clubs.032"), 13f, Theme.TextDim);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 4f;
            intro.Add(hint);
            _body.Add(intro);

            // Вступить по коду.
            var join = Card();
            join.Add(Ui.SectionTitle(LocalizationService.Get("menu.clubs.033")));
            var joinRow = Ui.Row();
            joinRow.style.alignItems = Align.Center;
            var codeField = Field(_code, 8, LocalizationService.Get("menu.clubs.034"));
            codeField.style.width = 220f;
            codeField.RegisterValueChangedCallback(e => _code = e.newValue);
            joinRow.Add(codeField);
            var joinButton = Ui.Button(LocalizationService.Get("menu.clubs.035"), () =>
            {
                if (string.IsNullOrWhiteSpace(_code)) { Fail(LocalizationService.Get("menu.clubs.036")); return; }
                SetStatus(LocalizationService.Get("menu.clubs.037"), Theme.TextDim);
                SocialCloud.JoinClub(_code.Trim(), null, Fail);
            }, ButtonKind.Primary, 14f);
            joinButton.style.marginLeft = 10f;
            joinRow.Add(joinButton);
            join.Add(joinRow);
            _body.Add(join);

            // Открытые клубы.
            var find = Card();
            find.Add(Ui.SectionTitle(LocalizationService.Get("menu.clubs.038")));
            var searchRow = Ui.Row();
            searchRow.style.alignItems = Align.Center;
            searchRow.style.marginBottom = 8f;
            var search = Field(_query, 32, LocalizationService.Get("menu.clubs.039"));
            search.style.width = 320f;
            search.RegisterValueChangedCallback(e => _query = e.newValue);
            searchRow.Add(search);
            var searchButton = Ui.Button(LocalizationService.Get("menu.clubs.040"), Search, ButtonKind.Secondary, 14f);
            searchButton.style.marginLeft = 10f;
            searchRow.Add(searchButton);
            find.Add(searchRow);
            if (_found == null && !_searching) Search();
            if (_searching) find.Add(Ui.Text(LocalizationService.Get("menu.clubs.041"), 13f, Theme.TextDim));
            else if (_found != null && _found.Length == 0) find.Add(Ui.Text(LocalizationService.Get("menu.clubs.042"), 13f, Theme.TextDim));
            foreach (var club in _found ?? new SocialCloud.ClubBrief[0])
            {
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6f;
                row.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(row, 10f);
                Ui.Padding(row, 6f, 10f);
                var logo = new ClubLogo(club.logo, 38f);
                logo.style.marginRight = 10f;
                row.Add(logo);
                var name = Ui.Text($"{club.name}  [{club.tag}]", 15f, Theme.Text, true);
                name.style.flexGrow = 1f;
                row.Add(name);
                row.Add(Ui.Text((En ? $"{club.members} members" : $"{club.members} уч."), 13f, Theme.TextDim));
                string id = club.id;
                var enter = Ui.Button(LocalizationService.Get("menu.clubs.035"), () =>
                {
                    SetStatus(LocalizationService.Get("menu.clubs.037"), Theme.TextDim);
                    SocialCloud.JoinClub(null, id, Fail);
                }, ButtonKind.Secondary, 13f);
                enter.style.marginLeft = 10f;
                row.Add(enter);
                find.Add(row);
            }
            _body.Add(find);

            // Создать клуб.
            var create = Card();
            create.Add(Ui.SectionTitle(LocalizationService.Get("menu.clubs.043")));
            var fields = Ui.Row();
            fields.style.alignItems = Align.Center;
            var nameField = Field(_newName, 32, LocalizationService.Get("menu.clubs.044"));
            nameField.style.width = 360f;
            nameField.RegisterValueChangedCallback(e => _newName = e.newValue);
            fields.Add(nameField);
            var tagField = Field(_newTag, 4, LocalizationService.Get("menu.clubs.045"));
            tagField.style.width = 110f;
            tagField.style.marginLeft = 10f;
            tagField.RegisterValueChangedCallback(e => _newTag = e.newValue.ToUpperInvariant());
            fields.Add(tagField);
            var openButton = Ui.Button(_newOpen ? LocalizationService.Get("menu.clubs.046") : LocalizationService.Get("menu.clubs.047"), () => { _newOpen = !_newOpen; Rebuild(); }, ButtonKind.Ghost, 13f);
            openButton.style.marginLeft = 10f;
            fields.Add(openButton);
            create.Add(fields);
            var logosRow = Ui.Row();
            logosRow.style.marginTop = 10f;
            logosRow.style.flexWrap = Wrap.Wrap;
            foreach (var starter in ClubLogo.Starters)
            {
                string logoId = starter;
                var cell = Ui.Box();
                cell.pickingMode = PickingMode.Position;
                Ui.Padding(cell, 6f, 6f);
                Ui.Radius(cell, 10f);
                cell.style.marginRight = 6f;
                cell.style.backgroundColor = logoId == _newLogo ? new Color(1f, 0.8f, 0.3f, 0.22f) : Theme.PanelLight;
                cell.Add(new ClubLogo(logoId, 48f));
                cell.RegisterCallback<ClickEvent>(_ =>
                {
                    Ui.ButtonClicked?.Invoke();
                    _newLogo = logoId;
                    Rebuild();
                });
                logosRow.Add(cell);
            }
            create.Add(logosRow);
            var createButton = Ui.Button(LocalizationService.Get("menu.clubs.043"), () =>
            {
                if ((_newName ?? "").Trim().Length < 3) { Fail(LocalizationService.Get("menu.clubs.048")); return; }
                if ((_newTag ?? "").Trim().Length < 2) { Fail(LocalizationService.Get("menu.clubs.049")); return; }
                SetStatus(LocalizationService.Get("menu.clubs.050"), Theme.TextDim);
                SocialCloud.CreateClub(_newName.Trim(), _newTag.Trim(), _newLogo, _newOpen, Fail);
            }, ButtonKind.Primary, 16f);
            createButton.style.marginTop = 12f;
            createButton.style.alignSelf = Align.FlexStart;
            createButton.style.minWidth = 220f;
            create.Add(createButton);
            _body.Add(create);
        }

        void Search()
        {
            // A later query must win even when the previous HTTP request
            // completes after it. Reject results from old Steam sessions.
            unchecked { _searchRevision++; }
            int requestId = _searchRevision;
            string owner = SocialCloud.CurrentSteamAccount;
            int generation = SocialCloud.CurrentAccountGeneration;
            string query = _query;
            _searching = true;
            SocialCloud.ListClubs(query, clubs =>
            {
                if (requestId != _searchRevision ||
                    !SocialCloud.BelongsToCurrentSession(owner, generation)) return;
                _searching = false;
                _found = clubs;
                Rebuild();
            }, error =>
            {
                if (requestId != _searchRevision ||
                    !SocialCloud.BelongsToCurrentSession(owner, generation)) return;
                _searching = false;
                // Keep existing valid search results on transient errors.
                Fail(error);
            });
        }

        // ================================================================== Рекорды друзей

        void BuildFriends()
        {
            var card = Card();
            card.Add(Ui.SectionTitle(LocalizationService.Get("menu.clubs.051")));
            var records = SocialCloud.FriendRecords;
            if (records.Count == 0)
            {
                var empty = Ui.Text(LocalizationService.Get("menu.clubs.052") +
                                    LocalizationService.Get("menu.clubs.053"), 13f, Theme.TextDim);
                empty.style.whiteSpace = WhiteSpace.Normal;
                card.Add(empty);
            }
            foreach (var group in records.GroupBy(r => (r.mode, r.mapPath)).OrderBy(g => g.First().mapName))
            {
                var first = group.First();
                var groupTitle = Ui.Text($"{first.mapName} · {(first.mode == "coop" ? LocalizationService.Get("menu.clubs.054") : LocalizationService.Get("menu.clubs.055"))}", 15f, Theme.Gold, true);
                groupTitle.style.marginTop = 10f;
                groupTitle.style.marginBottom = 4f;
                card.Add(groupTitle);
                foreach (var record in group.OrderByDescending(r => r.score).Take(6)) card.Add(RecordRow(record, "friend"));
            }
            _body.Add(card);
            BuildLocalRecords();
        }

        /// <summary>Рекорды других профилей этого компьютера (по сохранённым повторам).</summary>
        void BuildLocalRecords()
        {
            string me = Profiles.Active?.name ?? "";
            var entries = Replays.Entries.Where(e => !string.IsNullOrEmpty(e.author))
                .GroupBy(e => (e.mode, e.mapPath, (e.author ?? "").ToLowerInvariant()))
                .Select(g => g.OrderByDescending(e => e.authorScore).First())
                .OrderBy(e => e.map).ThenByDescending(e => e.authorScore).ToList();
            if (entries.Count == 0) return;
            var card = Card();
            card.Add(Ui.SectionTitle(LocalizationService.Get("menu.clubs.056")));
            foreach (var entry in entries.Take(20))
            {
                bool self = string.Equals(entry.author, me, StringComparison.OrdinalIgnoreCase);
                var ghost = Ghost.FromEntry(entry, self);
                card.Add(GhostRow(ghost, $"{entry.map} · {(entry.mode == "coop" ? LocalizationService.Get("menu.clubs.054") : LocalizationService.Get("menu.clubs.055"))}"));
            }
            _body.Add(card);
        }

        VisualElement RecordRow(SocialCloud.RecordInfo record, string source)
        {
            var ghost = Ghost.FromRecord(record, source);
            return GhostRow(ghost, $"{record.mapName} · {(record.mode == "coop" ? LocalizationService.Get("menu.clubs.054") : LocalizationService.Get("menu.clubs.055"))}");
        }

        VisualElement GhostRow(Ghost ghost, string where)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6f;
            row.style.backgroundColor = Theme.PanelLight;
            Ui.Radius(row, 10f);
            Ui.Padding(row, 6f, 12f);
            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            texts.Add(Ui.Text($"{(ghost.Source == "self" ? LocalizationService.Get("menu.clubs.057") : ghost.Name)} — {ghost.Score} очк.{(ghost.Won ? LocalizationService.Get("menu.clubs.058") : "")}", 15f, Theme.Text, true));
            texts.Add(Ui.Text(where + (string.IsNullOrEmpty(ghost.Date) ? "" : " · " + ShortDate(ghost.Date)), 12f, Theme.TextDim));
            row.Add(texts);
            var watch = Ui.Button(LocalizationService.Get("menu.clubs.059"), () =>
            {
                SetStatus(LocalizationService.Get("menu.clubs.060"), Theme.TextDim);
                ghost.LoadReplay(replay => Replays.Play?.Invoke(replay), Fail);
            }, ButtonKind.Secondary, 13f);
            watch.style.marginLeft = 8f;
            row.Add(watch);
            var raid = Ui.Button(ghost.Source == "self" ? LocalizationService.Get("menu.clubs.061") : LocalizationService.Get("menu.clubs.062"), () =>
            {
                SetStatus(LocalizationService.Get("menu.clubs.063"), Theme.TextDim);
                ghost.LoadReplay(_ => _app.StartRaid(ghost), Fail);
            }, ButtonKind.Primary, 13f);
            raid.style.marginLeft = 8f;
            raid.tooltip = En ? "Raid: play the same map, deck and opponents as the record holder, taking their seat." : "Рейд: та же карта, та же колода и те же соперники, что в партии рекордсмена. Вы садитесь на его место.";
            row.Add(raid);
            return row;
        }

        // ================================================================== Мелочи

        static VisualElement ProgressBar(int value, int target, Color color)
        {
            var bar = Ui.Box();
            bar.style.height = 16f;
            bar.style.marginTop = 10f;
            bar.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            Ui.Radius(bar, 8f);
            var fill = Ui.Box();
            fill.style.height = 16f;
            fill.style.width = Length.Percent(Mathf.Clamp01(target > 0 ? value / (float)target : 0f) * 100f);
            fill.style.backgroundColor = color;
            Ui.Radius(fill, 8f);
            bar.Add(fill);
            return bar;
        }

        static TextField Field(string value, int maxLength, string placeholder)
        {
            var field = new TextField { value = value ?? "", maxLength = maxLength };
            field.textEdition.placeholder = placeholder;
            field.style.height = 38f;
            field.style.fontSize = 15f;
            var input = field.Q(className: TextField.inputUssClassName) ?? field;
            input.style.backgroundColor = Theme.PanelLight;
            input.style.color = Theme.Text;
            Ui.Radius(input, 8f);
            Ui.Border(input, 1f, Theme.Line);
            Ui.Padding(input, 4f, 10f);
            return field;
        }

        static string EndsIn(string iso)
        {
            if (!DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var end)) return "";
            var left = end.ToUniversalTime() - DateTime.UtcNow;
            if (left.TotalHours < 1) return LocalizationService.Get("menu.clubs.064");
            if (left.TotalDays < 1) return En ? $"{(int)left.TotalHours}h left this week" : $"до конца недели {(int)left.TotalHours} ч";
            return En ? $"{(int)left.TotalDays}d {left.Hours}h left this week" : $"до конца недели {(int)left.TotalDays} дн. {left.Hours} ч";
        }

        static string ShortDate(string date) =>
            DateTime.TryParse(date, out var d) ? d.ToString("dd.MM.yyyy") : date ?? "";
    }
}
