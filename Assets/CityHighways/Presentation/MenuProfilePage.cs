using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Профиль»: профили этого компьютера, кошелёк влияния, статистика и магазин оформления
    /// (эмблема у имени, титул под именем, рамка строки игрока). Профиль привязан к имени игрока:
    /// игрок с таким именем в «Новой игре» получает его оформление и пополняет его кошелёк.
    /// </summary>
    public sealed class MenuProfilePage
    {
        readonly GameSetup _setup;
        readonly Action _back;
        readonly Action _setupChanged;
        readonly VisualElement _content;
        string _status = "";

        public VisualElement Root { get; }

        public MenuProfilePage(GameSetup setup, Action back, Action setupChanged)
        {
            _setup = setup;
            _back = back;
            _setupChanged = setupChanged;
            Root = Ui.Column();

            var header = Ui.Row();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 14f;
            var backButton = Ui.Button("← Назад", () => _back(), ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            header.Add(backButton);
            header.Add(Ui.Text("Профиль", 34f, Theme.Accent, true));
            Root.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 760f;
            Root.Add(scroll);
            _content = Ui.Column();
            scroll.Add(_content);
            Rebuild();
        }

        public void Rebuild()
        {
            _content.Clear();
            var profile = Profiles.Active;

            // ---------------------------------------------------------------- Аватар
            var avatarCard = Ui.Card(14f);
            avatarCard.style.marginBottom = 12f;
            avatarCard.style.flexDirection = FlexDirection.Row;
            avatarCard.style.alignItems = Align.Center;
            var avatar = new AvatarView(96f);
            avatar.Set(profile);
            avatar.style.marginRight = 18f;
            avatarCard.Add(avatar);
            var avatarSide = Ui.Column();
            avatarSide.style.flexGrow = 1f;
            avatarSide.Add(Ui.Text(profile.name, 24f, Theme.Text, true));
            var avatarNote = Ui.Text(profile.IsSteam
                ? "Профиль Steam: ник и аватар берутся из Steam. Можно поставить и свою картинку — она заменит аватар Steam."
                : "Загрузите картинку PNG или JPG — она обрежется до квадрата по центру.", 12f, Theme.TextDim);
            avatarNote.style.whiteSpace = WhiteSpace.Normal;
            avatarNote.style.marginTop = 4f;
            avatarSide.Add(avatarNote);
            var avatarButtons = Ui.Row();
            avatarButtons.style.marginTop = 10f;
            avatarButtons.style.alignItems = Align.Center;
            if (FilePicker.Supported)
            {
                avatarButtons.Add(Ui.Button("Загрузить картинку…", () =>
                {
                    string path = FilePicker.PickImage();
                    if (path == null) return;
                    _status = Profiles.ImportAvatar(profile, path) ?? "Аватар обновлён.";
                    Rebuild();
                }, ButtonKind.Primary, 14f));
            }
            else
            {
                var pathField = new TextField { value = "" };
                pathField.style.width = 360f;
                avatarButtons.Add(pathField);
                var load = Ui.Button("Загрузить по пути", () =>
                {
                    _status = Profiles.ImportAvatar(profile, pathField.value) ?? "Аватар обновлён.";
                    Rebuild();
                }, ButtonKind.Primary, 14f);
                load.style.marginLeft = 8f;
                avatarButtons.Add(load);
            }
            if (!string.IsNullOrEmpty(profile.avatar))
            {
                var removeAvatar = Ui.Button(profile.IsSteam ? "Вернуть аватар Steam" : "Убрать картинку", () =>
                {
                    Profiles.RemoveAvatar(profile);
                    _status = "Картинка убрана.";
                    Rebuild();
                }, ButtonKind.Ghost, 14f);
                removeAvatar.style.marginLeft = 10f;
                avatarButtons.Add(removeAvatar);
            }
            avatarSide.Add(avatarButtons);
            avatarCard.Add(avatarSide);
            _content.Add(avatarCard);
            // Аватар Steam мог ещё не догрузиться — заглянем ещё раз.
            if (profile.IsSteam && Profiles.AvatarOf(profile) == null)
                avatar.schedule.Execute(() => avatar.Set(profile)).Every(500).Until(() => Profiles.AvatarOf(profile) != null);

            // ---------------------------------------------------------------- Выбор профиля
            var chooser = Ui.Card(14f);
            chooser.style.marginBottom = 12f;
            var chips = Ui.Row();
            chips.style.flexWrap = Wrap.Wrap;
            chips.style.alignItems = Align.Center;
            chips.Add(Caption("Профили:", 15f, Theme.TextDim, 10f));
            foreach (var other in Profiles.Book.profiles)
            {
                var chosen = other;
                bool active = other == profile;
                var chip = Ui.Button(other.name, () => { Profiles.SetActive(chosen); _status = ""; Rebuild(); },
                    active ? ButtonKind.Primary : ButtonKind.Secondary, 14f);
                chip.style.marginRight = 8f;
                chip.style.marginBottom = 6f;
                chips.Add(chip);
            }
            chooser.Add(chips);

            var createRow = Ui.Row();
            createRow.style.alignItems = Align.Center;
            createRow.style.marginTop = 8f;
            var nameField = new TextField { value = "", maxLength = 16 };
            nameField.style.width = 260f;
            nameField.style.fontSize = 15f;
            createRow.Add(nameField);
            var create = Ui.Button("Новый профиль", () =>
            {
                string name = (nameField.value ?? "").Trim();
                if (name.Length == 0) { _status = "Введите имя нового профиля."; Rebuild(); return; }
                if (Profiles.Find(name) != null) { _status = "Такой профиль уже есть."; Rebuild(); return; }
                Profiles.SetActive(Profiles.GetOrCreate(name));
                _status = $"Профиль «{name}» создан.";
                Rebuild();
            }, ButtonKind.Secondary, 14f);
            create.style.marginLeft = 10f;
            createRow.Add(create);
            var play = Ui.Button("Играть под этим именем", () =>
            {
                var first = _setup.players.FirstOrDefault(p => !p.isBot);
                if (first != null) first.name = profile.name;
                _setup.Save();
                _setupChanged?.Invoke();
                _status = $"В «Новой игре» первый игрок теперь «{profile.name}».";
                Rebuild();
            }, ButtonKind.Secondary, 14f);
            play.style.marginLeft = 10f;
            createRow.Add(play);
            if (Profiles.Book.profiles.Count > 1)
            {
                var remove = Ui.Button("Удалить профиль", () => { Profiles.Remove(profile); _status = $"Профиль «{profile.name}» удалён."; Rebuild(); },
                    ButtonKind.Ghost, 14f);
                remove.style.marginLeft = 10f;
                createRow.Add(remove);
            }
            chooser.Add(createRow);
            if (_status.Length > 0)
            {
                var status = Ui.Text(_status, 13f, Theme.Accent);
                status.style.marginTop = 6f;
                chooser.Add(status);
            }
            _content.Add(chooser);

            // ---------------------------------------------------------------- Карточка игрока и статистика
            var summary = Ui.Row();
            summary.style.alignItems = Align.Stretch;
            summary.style.marginBottom = 12f;

            var preview = Ui.Card(14f);
            preview.style.flexGrow = 1f;
            preview.style.flexBasis = 0f;
            preview.style.marginRight = 12f;
            preview.Add(Ui.Text("Так вас видят за столом", 12f, Theme.TextDim));
            var frame = Profiles.FrameColor(profile.frame);
            var row = Ui.Column();
            row.style.marginTop = 8f;
            row.style.backgroundColor = Theme.PanelLight;
            Ui.Radius(row, 8f);
            Ui.Padding(row, 8f, 10f);
            Ui.Border(row, frame.a > 0f ? 2f : 1f, frame.a > 0f ? frame : Theme.Line);
            var line = Ui.Row();
            line.style.alignItems = Align.Center;
            var badge = new ProfileBadge(18f);
            badge.Set(profile.emblem, frame.a > 0f ? frame : new Color(0.98f, 0.78f, 0.25f));
            badge.style.marginRight = 6f;
            line.Add(badge);
            line.Add(Ui.Text(profile.name, 18f, Theme.Text, true));
            row.Add(line);
            var title = Profiles.TitleText(profile.title);
            if (title.Length > 0) row.Add(Ui.Text(title, 12f, new Color(0.98f, 0.78f, 0.25f), true));
            preview.Add(row);
            summary.Add(preview);

            var stats = Ui.Card(14f);
            stats.style.flexGrow = 1f;
            stats.style.flexBasis = 0f;
            stats.Add(Ui.Text($"Кошелёк: {profile.wallet} влияния", 22f, Theme.Accent, true));
            stats.Add(Caption($"Партий: {profile.games} · побед: {profile.wins} · влияния заработано всего: {profile.earnedTotal}", 14f, Theme.Text, 0f));
            var how = Ui.Text("Влияние, которое осталось у вас к концу партии, попадает сюда. За одним компьютером профиль узнаётся по имени игрока, в сетевой игре — это активный профиль.", 12f, Theme.TextDim);
            how.style.whiteSpace = WhiteSpace.Normal;
            how.style.marginTop = 6f;
            stats.Add(how);
            summary.Add(stats);
            _content.Add(summary);

            // ---------------------------------------------------------------- Магазин
            Shop(profile, ShopKind.Emblem, "Эмблемы — у имени");
            Shop(profile, ShopKind.Title, "Титулы — под именем");
            Shop(profile, ShopKind.Frame, "Рамки — вокруг строки игрока");
        }

        void Shop(PlayerProfile profile, ShopKind kind, string caption)
        {
            var card = Ui.Card(14f);
            card.style.marginBottom = 12f;
            card.Add(Ui.Text(caption, 17f, Theme.Text, true));
            var items = Ui.Row();
            items.style.flexWrap = Wrap.Wrap;
            items.style.marginTop = 8f;
            foreach (var item in Profiles.Catalog.Where(i => i.Kind == kind))
            {
                var chosen = item;
                bool owned = profile.Owns(item.Id);
                bool equipped = profile.Equipped(kind) == item.Id;
                var cell = Ui.Column();
                cell.style.width = 170f;
                cell.style.marginRight = 10f;
                cell.style.marginBottom = 10f;
                cell.style.backgroundColor = equipped ? Theme.PanelHover : Theme.PanelLight;
                cell.style.alignItems = Align.Center;
                Ui.Radius(cell, 8f);
                Ui.Padding(cell, 10f, 8f);
                var frame = kind == ShopKind.Frame ? Profiles.FrameColor(item.Id) : Theme.Line;
                Ui.Border(cell, equipped || kind == ShopKind.Frame ? 2f : 1f, kind == ShopKind.Frame ? frame : equipped ? Theme.Accent : Theme.Line);

                if (kind == ShopKind.Emblem)
                {
                    var badge = new ProfileBadge(28f);
                    badge.Set(item.Id, new Color(0.98f, 0.78f, 0.25f));
                    cell.Add(badge);
                }
                var name = Ui.Text(item.Name, 14f, kind == ShopKind.Title ? new Color(0.98f, 0.78f, 0.25f) : Theme.Text, true);
                name.style.marginTop = 4f;
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                cell.Add(name);

                UnityEngine.UIElements.Button button;
                if (equipped)
                    button = Ui.Button("Снять", () => { profile.Equip(kind, ""); Profiles.Save(); Rebuild(); }, ButtonKind.Ghost, 13f);
                else if (owned)
                    button = Ui.Button("Надеть", () => { profile.Equip(kind, chosen.Id); Profiles.Save(); Rebuild(); }, ButtonKind.Secondary, 13f);
                else
                {
                    button = Ui.Button($"Купить · {item.Price}", () =>
                    {
                        var error = Profiles.Buy(profile, chosen);
                        _status = error ?? $"Куплено: {chosen.Name}.";
                        Rebuild();
                    }, ButtonKind.Primary, 13f);
                    Ui.SetEnabled(button, profile.wallet >= item.Price);
                }
                button.style.marginTop = 6f;
                cell.Add(button);
                items.Add(cell);
            }
            card.Add(items);
            _content.Add(card);
        }

        static Label Caption(string text, float size, Color color, float marginRight)
        {
            var label = Ui.Text(text, size, color);
            label.style.marginRight = marginRight;
            return label;
        }
    }
}
