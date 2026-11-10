using System.Linq;
using RailGame.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RailGame.Presentation.App
{
    /// <summary>Главное меню: состав игроков, число вагонов, звук и скрытие карт.</summary>
    public sealed class MainMenuView
    {
        const int MinPlayers = 2;
        const int MaxPlayers = 5;
        const int MinTrains = 15;
        const int MaxTrains = 45;

        readonly RailGameApp _app;
        readonly GameSetup _setup;
        readonly VisualElement _playersList;
        readonly Label _trainsValue;
        readonly UnityEngine.UIElements.Button _hideCardsButton;
        readonly UnityEngine.UIElements.Button _soundButton;
        readonly UnityEngine.UIElements.Button _addPlayerButton;
        readonly Label _hint;

        public VisualElement Root { get; }

        public MainMenuView(RailGameApp app, GameSetup setup)
        {
            _app = app;
            _setup = setup;

            Root = new VisualElement();
            Root.style.flexGrow = 1f;
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            Root.style.backgroundColor = Theme.Background;

            AddDecorations();

            var column = Ui.Column();
            column.style.width = 720f;
            column.style.alignItems = Align.Stretch;
            Root.Add(column);

            var title = Ui.Text("Стальные магистрали", 58f, Theme.Accent, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            column.Add(title);
            var subtitle = Ui.Text("Прокладывайте железные дороги между городами и выполняйте задания", 17f, Theme.TextDim);
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginTop = 6f;
            subtitle.style.marginBottom = 34f;
            column.Add(subtitle);

            // ---------- Игроки
            var playersCard = Ui.Card(20f);
            playersCard.style.backgroundColor = Theme.Panel;
            playersCard.Add(Ui.SectionTitle("Игроки"));
            _playersList = Ui.Column();
            playersCard.Add(_playersList);
            _addPlayerButton = Ui.Button("+ Добавить игрока", AddPlayer, ButtonKind.Ghost, 14f);
            _addPlayerButton.style.marginTop = 6f;
            _addPlayerButton.style.alignSelf = Align.FlexStart;
            playersCard.Add(_addPlayerButton);
            column.Add(playersCard);

            // ---------- Настройки
            var settingsCard = Ui.Card(20f);
            settingsCard.style.backgroundColor = Theme.Panel;
            settingsCard.style.marginTop = 14f;
            settingsCard.Add(Ui.SectionTitle("Настройки"));

            var trainsRow = SettingRow("Вагонов у каждого игрока");
            var minus = Ui.Button("–", () => ChangeTrains(-5), ButtonKind.Secondary, 18f);
            minus.style.width = 44f;
            trainsRow.Add(minus);
            _trainsValue = Ui.Text("", 20f, null, true);
            _trainsValue.style.width = 60f;
            _trainsValue.style.unityTextAlign = TextAnchor.MiddleCenter;
            trainsRow.Add(_trainsValue);
            var plus = Ui.Button("+", () => ChangeTrains(5), ButtonKind.Secondary, 18f);
            plus.style.width = 44f;
            trainsRow.Add(plus);
            settingsCard.Add(trainsRow);

            var hideRow = SettingRow("Скрывать карты при передаче хода");
            _hideCardsButton = Ui.Button("", ToggleHideCards, ButtonKind.Secondary, 14f);
            _hideCardsButton.style.width = 148f;
            hideRow.Add(_hideCardsButton);
            settingsCard.Add(hideRow);

            var soundRow = SettingRow("Звук");
            _soundButton = Ui.Button("", ToggleSound, ButtonKind.Secondary, 14f);
            _soundButton.style.width = 148f;
            soundRow.Add(_soundButton);
            settingsCard.Add(soundRow);
            column.Add(settingsCard);

            // ---------- Старт
            var start = Ui.Button("Начать игру", StartGame, ButtonKind.Primary, 22f);
            start.style.marginTop = 24f;
            start.style.height = 58f;
            column.Add(start);

            _hint = Ui.Text("", 13f, Theme.TextDim);
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hint.style.marginTop = 14f;
            column.Add(_hint);

            Refresh();
        }

        static VisualElement SettingRow(string caption)
        {
            var row = Ui.Row();
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginBottom = 10f;
            var label = Ui.Text(caption, 16f);
            label.style.flexGrow = 1f;
            row.Add(label);
            return row;
        }

        /// <summary>Несколько «вагонов» по углам — чтобы меню не было пустым.</summary>
        void AddDecorations()
        {
            var colors = new[] { CardColor.Red, CardColor.Yellow, CardColor.Green, CardColor.Blue, CardColor.Purple, CardColor.Orange };
            for (int i = 0; i < colors.Length; i++)
            {
                var wagon = Ui.Box();
                wagon.style.position = Position.Absolute;
                wagon.style.width = 70f;
                wagon.style.height = 20f;
                wagon.style.backgroundColor = Palette.Card(colors[i]);
                wagon.style.opacity = 0.18f;
                Ui.Radius(wagon, 4f);
                bool top = i < 3;
                if (top)
                {
                    wagon.style.left = 60f + i * 84f;
                    wagon.style.top = 60f;
                }
                else
                {
                    wagon.style.right = 60f + (i - 3) * 84f;
                    wagon.style.bottom = 60f;
                }
                Root.Add(wagon);
            }
        }

        void Refresh()
        {
            _playersList.Clear();
            for (int i = 0; i < _setup.players.Count; i++) _playersList.Add(PlayerRow(i));

            Ui.Show(_addPlayerButton, _setup.players.Count < MaxPlayers);
            _trainsValue.text = _setup.trainsPerPlayer.ToString();
            _hideCardsButton.text = _setup.hideCardsBetweenTurns ? "Да" : "Нет";
            _soundButton.text = _setup.sound ? "Включён" : "Выключен";

            int humans = _setup.players.Count(p => !p.isBot);
            _hint.text = humans == 0
                ? "Все игроки — боты: можно просто смотреть партию."
                : humans == 1
                    ? "Совет: наведите курсор на маршрут — подсказка покажет, хватает ли карт."
                    : "Игра за одним компьютером: перед каждым ходом карты скрываются, пока игрок не нажмёт «Показать».";
        }

        VisualElement PlayerRow(int index)
        {
            var slot = _setup.players[index];
            var row = Ui.Row();
            row.style.marginBottom = 8f;

            var chip = Ui.Box();
            chip.style.width = 18f;
            chip.style.height = 18f;
            chip.style.marginRight = 12f;
            chip.style.backgroundColor = Palette.PlayerColor(index);
            Ui.Radius(chip, 4f);
            row.Add(chip);

            var field = new TextField();
            field.value = slot.name;
            field.maxLength = 16;
            field.style.flexGrow = 1f;
            field.style.marginRight = 10f;
            field.style.fontSize = 16f;
            field.style.color = Theme.Text;
            Ui.Margin(field, 0f);
            field.style.marginRight = 10f;
            // Без темы у поля нет фона — задаём сами.
            var input = field.Q(className: TextField.inputUssClassName) ?? field;
            input.style.backgroundColor = Theme.PanelLight;
            input.style.color = Theme.Text;
            input.style.minHeight = 38f;
            Ui.Radius(input, 8f);
            Ui.Border(input, 1f, Theme.Line);
            Ui.Padding(input, 6f, 10f);
            field.RegisterValueChangedCallback(e => slot.name = e.newValue);
            row.Add(field);

            var kind = Ui.Button(KindLabel(slot), () =>
            {
                CycleKind(slot);
                Refresh();
            }, ButtonKind.Secondary, 14f);
            kind.style.width = 190f;
            kind.style.marginRight = 8f;
            row.Add(kind);

            var remove = Ui.Button("×", () =>
            {
                _setup.players.RemoveAt(index);
                Refresh();
            }, ButtonKind.Ghost, 14f);
            remove.style.width = 44f;
            Ui.SetEnabled(remove, _setup.players.Count > MinPlayers);
            row.Add(remove);
            return row;
        }

        static string KindLabel(PlayerSlot slot)
        {
            if (!slot.isBot) return "Человек";
            return slot.botLevel == BotLevel.Random ? "Бот: случайный" : "Бот: умный";
        }

        static void CycleKind(PlayerSlot slot)
        {
            // Человек → умный бот → случайный бот → человек.
            if (!slot.isBot)
            {
                slot.isBot = true;
                slot.botLevel = BotLevel.Smart;
            }
            else if (slot.botLevel == BotLevel.Smart)
            {
                slot.botLevel = BotLevel.Random;
            }
            else
            {
                slot.isBot = false;
            }
        }

        void AddPlayer()
        {
            if (_setup.players.Count >= MaxPlayers) return;
            var name = GameSetup.DefaultNames.FirstOrDefault(n => _setup.players.All(p => p.name != n))
                       ?? "Игрок " + (_setup.players.Count + 1);
            _setup.players.Add(new PlayerSlot(name, true, BotLevel.Smart));
            Refresh();
        }

        void ChangeTrains(int delta)
        {
            _setup.trainsPerPlayer = Mathf.Clamp(_setup.trainsPerPlayer + delta, MinTrains, MaxTrains);
            Refresh();
        }

        void ToggleHideCards()
        {
            _setup.hideCardsBetweenTurns = !_setup.hideCardsBetweenTurns;
            Refresh();
        }

        void ToggleSound()
        {
            _setup.sound = !_setup.sound;
            _app.Sound.Enabled = _setup.sound;
            Refresh();
        }

        void StartGame() => _app.StartGame(_setup);
    }
}
