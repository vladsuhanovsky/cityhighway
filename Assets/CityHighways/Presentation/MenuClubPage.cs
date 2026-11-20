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
        enum Tab { Club, Friends }

        readonly CityHighwaysApp _app;
        readonly VisualElement _body;
        readonly Label _status;
        Tab _tab = Tab.Club;
        bool _searching;
        string _query = "";
        SocialCloud.ClubBrief[] _found;
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
            var backButton = Ui.Button("← Назад", back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            Ui.MarkBack(Root, back);
            header.Add(backButton);
            var title = Ui.Text("Клубы и друзья", 34f, Theme.Gold, true);
            title.style.flexGrow = 1f;
            header.Add(title);
            var refresh = Ui.Button("Обновить", () =>
            {
                SocialCloud.RefreshClub();
                SocialCloud.RefreshFriends();
                SetStatus("Обновляем…", Theme.TextDim);
            }, ButtonKind.Secondary, 13f);
            header.Add(refresh);
            Root.Add(header);

            var tabs = Ui.Row();
            tabs.style.marginBottom = 12f;
            AddTab(tabs, "Мой клуб", Tab.Club);
            AddTab(tabs, "Рекорды друзей", Tab.Friends);
            Root.Add(tabs);

            _status = Ui.Text("", 13f, Theme.TextDim);
            _status.style.marginBottom = 8f;
            _status.style.whiteSpace = WhiteSpace.Normal;
            Root.Add(_status);

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
                var hint = Ui.Text("Без Steam можно соревноваться с другими профилями этого компьютера: их рекорды видны в «Новой игре» над кнопкой «Начать игру», " +
                                   "а повторы партий — в «Счётчике партий».", 13f, Theme.TextDim);
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
                if (string.IsNullOrEmpty(SocialCloud.Problem)) SetStatus("Загружаем клуб…", Theme.TextDim);
                else Fail("Клуб не загрузился: " + SocialCloud.Problem + " Нажмите «Обновить», чтобы попробовать ещё раз.");
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
            titles.Add(Ui.Text($"Участников: {state.members?.Length ?? 0} из 30 · целей недели выполнено: {state.weeksDone}" +
                               (club.open ? " · открыт для всех" : " · только по коду"), 13f, Theme.TextDim));
            var code = Ui.Text($"Код приглашения: {club.code}", 15f, Theme.Text, true);
            code.style.marginTop = 6f;
            titles.Add(code);
            row.Add(titles);
            var buttons = Ui.Column();
            var copy = Ui.Button("Скопировать код", () =>
            {
                GUIUtility.systemCopyBuffer = club.code;
                SetStatus("Код скопирован — отправьте его друзьям.", Theme.Success);
            }, ButtonKind.Secondary, 13f);
            copy.style.marginBottom = 6f;
            buttons.Add(copy);
            if (club.owner)
            {
                var open = Ui.Button(club.open ? "Закрыть набор" : "Открыть набор", () => SocialCloud.SetClubOpen(!club.open, Fail), ButtonKind.Ghost, 13f);
                open.style.marginBottom = 6f;
                buttons.Add(open);
            }
            buttons.Add(Ui.Button("Выйти из клуба", () => SocialCloud.LeaveClub(Fail), ButtonKind.Ghost, 13f));
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
                var goalTitle = Ui.Text("Цель недели: " + SocialCloud.GoalTitle(week.kind, week.target), 18f, Theme.Text, true);
                goalTitle.style.flexGrow = 1f;
                goalHead.Add(goalTitle);
                goalHead.Add(Ui.Text(EndsIn(week.endsAt), 13f, Theme.TextDim));
                goal.Add(goalHead);
                var hint = Ui.Text(SocialCloud.GoalHint(week.kind) + " Награда — уникальный клубный логотип, он виден в лобби сетевой игры рядом с именами участников.",
                    12f, Theme.TextDim);
                hint.style.whiteSpace = WhiteSpace.Normal;
                hint.style.marginTop = 4f;
                goal.Add(hint);
                goal.Add(ProgressBar(week.progress, week.target, week.completed ? Theme.Success : Theme.Gold));
                var numbers = Ui.Text(week.completed
                    ? $"Выполнено: {week.progress} из {week.target} {SocialCloud.GoalUnit(week.kind, week.target)} — логотип получен!"
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
                    var name = Ui.Text(member.name + (member.role == "owner" ? " ♛" : "") + (member.me ? " (вы)" : ""), 14f, member.me ? Theme.Gold : Theme.Text, member.me);
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
                    var amount = Ui.Number($"{member.amount} · партий {member.games}", 12f, Theme.TextDim);
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
            logos.Add(Ui.SectionTitle("Логотипы клуба"));
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
                var caption = Ui.Text(string.IsNullOrEmpty(item.week) ? "стартовый" : $"{ClubLogo.TierTitle(item.logo)} · {item.week}", 11f, Theme.TextDim);
                caption.style.marginTop = 4f;
                caption.style.unityTextAlign = TextAnchor.MiddleCenter;
                caption.style.whiteSpace = WhiteSpace.Normal;
                cell.Add(caption);
                if (active) cell.Add(Ui.Text("герб клуба", 11f, Theme.Gold, true));
                else if (club.owner)
                {
                    string logoId = item.logo;
                    var use = Ui.Button("Сделать гербом", () => SocialCloud.SetClubLogo(logoId, Fail), ButtonKind.Ghost, 11f);
                    use.style.marginTop = 2f;
                    cell.Add(use);
                }
                logoRow.Add(cell);
            }
            logos.Add(logoRow);
            var logoHint = Ui.Text("Каждая выполненная цель недели приносит новый логотип — его форма, знак и цвет собраны из id клуба и недели, " +
                                   "так что такого больше ни у кого нет. Чем больше целей выполнено, тем выше ранг: бронза, серебро, золото.", 12f, Theme.TextDim);
            logoHint.style.whiteSpace = WhiteSpace.Normal;
            logos.Add(logoHint);
            _body.Add(logos);

            // Рекорды участников — повторы партий клубных товарищей.
            if (state.records != null && state.records.Length > 0)
            {
                var records = Card();
                records.Add(Ui.SectionTitle("Рекорды участников"));
                foreach (var record in state.records.Take(15))
                    records.Add(RecordRow(record, record.name == state.members?.FirstOrDefault(m => m.me)?.name ? "self" : "club"));
                _body.Add(records);
            }
        }

        void BuildNoClub()
        {
            if (_statusTransient) SetStatus("", Theme.TextDim);
            var intro = Card();
            intro.Add(Ui.Text("Вы пока не в клубе", 20f, Theme.Text, true));
            var hint = Ui.Text("Клуб — до 30 строителей с общей целью на неделю: построить столько-то километров дорог, выполнить контракты или заказы города, " +
                               "доставить грузы. Каждая ваша партия идёт в общий счёт. За выполненную цель клуб получает уникальный логотип — " +
                               "он виден в лобби сетевой игры рядом с именами участников.", 13f, Theme.TextDim);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 4f;
            intro.Add(hint);
            _body.Add(intro);

            // Вступить по коду.
            var join = Card();
            join.Add(Ui.SectionTitle("Вступить по коду приглашения"));
            var joinRow = Ui.Row();
            joinRow.style.alignItems = Align.Center;
            var codeField = Field(_code, 8, "Код из 6 символов");
            codeField.style.width = 220f;
            codeField.RegisterValueChangedCallback(e => _code = e.newValue);
            joinRow.Add(codeField);
            var joinButton = Ui.Button("Вступить", () =>
            {
                if (string.IsNullOrWhiteSpace(_code)) { Fail("Введите код приглашения."); return; }
                SetStatus("Вступаем…", Theme.TextDim);
                SocialCloud.JoinClub(_code.Trim(), null, Fail);
            }, ButtonKind.Primary, 14f);
            joinButton.style.marginLeft = 10f;
            joinRow.Add(joinButton);
            join.Add(joinRow);
            _body.Add(join);

            // Открытые клубы.
            var find = Card();
            find.Add(Ui.SectionTitle("Открытые клубы"));
            var searchRow = Ui.Row();
            searchRow.style.alignItems = Align.Center;
            searchRow.style.marginBottom = 8f;
            var search = Field(_query, 32, "Название или тег");
            search.style.width = 320f;
            search.RegisterValueChangedCallback(e => _query = e.newValue);
            searchRow.Add(search);
            var searchButton = Ui.Button("Найти", Search, ButtonKind.Secondary, 14f);
            searchButton.style.marginLeft = 10f;
            searchRow.Add(searchButton);
            find.Add(searchRow);
            if (_found == null && !_searching) Search();
            if (_searching) find.Add(Ui.Text("Ищем…", 13f, Theme.TextDim));
            else if (_found != null && _found.Length == 0) find.Add(Ui.Text("Открытых клубов не нашлось — создайте свой!", 13f, Theme.TextDim));
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
                row.Add(Ui.Text($"{club.members} уч.", 13f, Theme.TextDim));
                string id = club.id;
                var enter = Ui.Button("Вступить", () =>
                {
                    SetStatus("Вступаем…", Theme.TextDim);
                    SocialCloud.JoinClub(null, id, Fail);
                }, ButtonKind.Secondary, 13f);
                enter.style.marginLeft = 10f;
                row.Add(enter);
                find.Add(row);
            }
            _body.Add(find);

            // Создать клуб.
            var create = Card();
            create.Add(Ui.SectionTitle("Создать клуб"));
            var fields = Ui.Row();
            fields.style.alignItems = Align.Center;
            var nameField = Field(_newName, 32, "Название (3–32 символа)");
            nameField.style.width = 360f;
            nameField.RegisterValueChangedCallback(e => _newName = e.newValue);
            fields.Add(nameField);
            var tagField = Field(_newTag, 4, "Тег");
            tagField.style.width = 110f;
            tagField.style.marginLeft = 10f;
            tagField.RegisterValueChangedCallback(e => _newTag = e.newValue.ToUpperInvariant());
            fields.Add(tagField);
            var openButton = Ui.Button(_newOpen ? "Открыт для всех" : "Только по коду", () => { _newOpen = !_newOpen; Rebuild(); }, ButtonKind.Ghost, 13f);
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
            var createButton = Ui.Button("Создать клуб", () =>
            {
                if ((_newName ?? "").Trim().Length < 3) { Fail("Название клуба — от 3 символов."); return; }
                if ((_newTag ?? "").Trim().Length < 2) { Fail("Тег — 2–4 буквы или цифры."); return; }
                SetStatus("Создаём клуб…", Theme.TextDim);
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
            _searching = true;
            SocialCloud.ListClubs(_query, clubs =>
            {
                _searching = false;
                _found = clubs;
                Rebuild();
            }, error =>
            {
                _searching = false;
                _found = new SocialCloud.ClubBrief[0];
                Fail(error);
            });
        }

        // ================================================================== Рекорды друзей

        void BuildFriends()
        {
            var card = Card();
            card.Add(Ui.SectionTitle("Рекорды друзей из Steam"));
            var records = SocialCloud.FriendRecords;
            if (records.Count == 0)
            {
                var empty = Ui.Text("Пока ни у кого из друзей нет рекордов. Рекорд появляется после партии в соревновании или кооперативе — " +
                                    "позовите друзей сыграть, и их лучшие результаты будут здесь.", 13f, Theme.TextDim);
                empty.style.whiteSpace = WhiteSpace.Normal;
                card.Add(empty);
            }
            foreach (var group in records.GroupBy(r => (r.mode, r.mapPath)).OrderBy(g => g.First().mapName))
            {
                var first = group.First();
                var groupTitle = Ui.Text($"{first.mapName} · {(first.mode == "coop" ? "кооператив" : "соревнование")}", 15f, Theme.Gold, true);
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
            card.Add(Ui.SectionTitle("Рекорды на этом компьютере"));
            foreach (var entry in entries.Take(20))
            {
                bool self = string.Equals(entry.author, me, StringComparison.OrdinalIgnoreCase);
                var ghost = Ghost.FromEntry(entry, self);
                card.Add(GhostRow(ghost, $"{entry.map} · {(entry.mode == "coop" ? "кооператив" : "соревнование")}"));
            }
            _body.Add(card);
        }

        VisualElement RecordRow(SocialCloud.RecordInfo record, string source)
        {
            var ghost = Ghost.FromRecord(record, source);
            return GhostRow(ghost, $"{record.mapName} · {(record.mode == "coop" ? "кооператив" : "соревнование")}");
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
            texts.Add(Ui.Text($"{(ghost.Source == "self" ? "Вы" : ghost.Name)} — {ghost.Score} очк.{(ghost.Won ? " · победа" : "")}", 15f, Theme.Text, true));
            texts.Add(Ui.Text(where + (string.IsNullOrEmpty(ghost.Date) ? "" : " · " + ShortDate(ghost.Date)), 12f, Theme.TextDim));
            row.Add(texts);
            var watch = Ui.Button("▶ Повтор", () =>
            {
                SetStatus("Загружаем повтор…", Theme.TextDim);
                ghost.LoadReplay(replay => Replays.Play?.Invoke(replay), Fail);
            }, ButtonKind.Secondary, 13f);
            watch.style.marginLeft = 8f;
            row.Add(watch);
            var raid = Ui.Button(ghost.Source == "self" ? "Побить свой рекорд" : "Бросить вызов", () =>
            {
                SetStatus("Готовим рейд…", Theme.TextDim);
                ghost.LoadReplay(_ => _app.StartRaid(ghost), Fail);
            }, ButtonKind.Primary, 13f);
            raid.style.marginLeft = 8f;
            raid.tooltip = "Рейд: та же карта, та же колода и те же соперники, что в партии рекордсмена. Вы садитесь на его место.";
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
            if (left.TotalHours < 1) return "до конца недели меньше часа";
            if (left.TotalDays < 1) return $"до конца недели {(int)left.TotalHours} ч";
            return $"до конца недели {(int)left.TotalDays} дн. {left.Hours} ч";
        }

        static string ShortDate(string date) =>
            DateTime.TryParse(date, out var d) ? d.ToString("dd.MM.yyyy") : date ?? "";
    }
}
