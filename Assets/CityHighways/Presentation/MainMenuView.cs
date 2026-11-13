using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Главное меню из страниц: главная (кнопки), «Новая игра» (игроки и параметры партии),
    /// «Правила» (вкладки с картинками), «Счётчик партий» и «Настройки» (экран и звук).
    /// </summary>
    public sealed class MainMenuView
    {
        const int MinPlayers = 2;
        const int MaxPlayers = 5;
        /// <summary>Варианты числа участков: 0 — «по правилам» (зависит от числа игроков).</summary>
        static readonly int[] PieceOptions = { 0, 15, 20, 25, 30, 35, 40 };

        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        readonly DisplaySettings _display;
        readonly List<Vector2Int> _resolutions;

        readonly VisualElement _playersList;
        readonly UnityEngine.UIElements.Button _addPlayerButton;
        readonly Label _characterHint;
        int _lastCharacterSlot = -1;
        readonly Label _trainsValue;
        readonly UnityEngine.UIElements.Button _hideCardsButton;
        readonly UnityEngine.UIElements.Button _timerButton;
        readonly UnityEngine.UIElements.Button _soundButton;
        readonly UnityEngine.UIElements.Button _screenModeButton;
        readonly UnityEngine.UIElements.Button _resolutionButton;
        readonly Label _volumeValue;
        readonly Label _hint;
        readonly VisualElement _overlay;

        enum Page { Home, NewGame, Online, Profile, Rules, Stats, Settings }
        readonly Dictionary<Page, VisualElement> _pages = new Dictionary<Page, VisualElement>();
        readonly VisualElement _pageHost;
        readonly MenuOnlinePage _online;
        readonly MenuProfilePage _profile;

        public VisualElement Root { get; }

        public MainMenuView(CityHighwaysApp app, GameSetup setup)
        {
            _app = app;
            _setup = setup;
            _display = app.Display ?? DisplaySettings.Load();
            _resolutions = DisplaySettings.AvailableResolutions();

            Root = new VisualElement();
            Root.style.flexGrow = 1f;
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            Root.style.backgroundColor = Theme.Background;

            AddDecorations();
            Profiles.SyncSteam();

            _pageHost = Ui.Column();
            _pageHost.style.width = 1120f;
            _pageHost.style.alignItems = Align.Stretch;
            Root.Add(_pageHost);

            var saved = SaveSystem.Load();
            _pages[Page.Home] = BuildHome(saved);
            _pages[Page.NewGame] = BuildNewGame(saved, out _playersList, out _addPlayerButton, out _characterHint,
                out _trainsValue, out _hideCardsButton, out _timerButton, out _hint);
            _online = new MenuOnlinePage(app, setup, () => ShowPage(Page.Home));
            _pages[Page.Online] = _online.Root;
            _profile = new MenuProfilePage(setup, () => ShowPage(Page.Home), Refresh);
            _pages[Page.Profile] = _profile.Root;
            _pages[Page.Rules] = MenuRulesPage.Build(new GameConfig(), () => ShowPage(Page.Home));
            _pages[Page.Stats] = MenuStatsPage.Build(() => ShowPage(Page.Home), () => ShowPage(Page.Stats), RebuildStats);
            _pages[Page.Settings] = BuildSettings(out _screenModeButton, out _resolutionButton, out _soundButton, out _volumeValue);
            foreach (var page in _pages.Values) _pageHost.Add(page);

            _overlay = new VisualElement();
            Ui.Fill(_overlay);
            _overlay.style.backgroundColor = Theme.Shade;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            Ui.Show(_overlay, false);

            _profileCorner = BuildProfileCorner();
            Root.Add(_profileCorner);
            Root.Add(_overlay);

            ShowPage(Page.Home);
            Refresh();
        }

        /// <summary>Друг пригласил нас в лобби Steam — открываем «Сетевую игру» и входим.</summary>
        public void JoinSteamLobby(ulong lobby)
        {
            ShowPage(Page.Online);
            _online.JoinSteam(lobby);
        }

        void ShowPage(Page page)
        {
            foreach (var pair in _pages) Ui.Show(pair.Value, pair.Key == page);
            if (_profileCorner != null)
            {
                // Аватар в углу — на главной; на остальных страницах он только мешал бы.
                Ui.Show(_profileCorner, page == Page.Home);
                if (page == Page.Home) RefreshProfileCorner();
            }
        }

        VisualElement _profileCorner;
        AvatarView _cornerAvatar;
        Label _cornerName;
        Label _cornerWallet;

        /// <summary>Аватар профиля в левом нижнем углу главного меню: клик — страница «Профиль».</summary>
        VisualElement BuildProfileCorner()
        {
            var corner = Ui.Row();
            corner.style.position = Position.Absolute;
            corner.style.left = 28f;
            corner.style.bottom = 28f;
            corner.style.alignItems = Align.Center;
            corner.style.backgroundColor = Theme.Panel;
            Ui.Radius(corner, 14f);
            Ui.Padding(corner, 10f, 12f);
            Ui.Border(corner, 1f, Theme.Line);
            corner.pickingMode = PickingMode.Position;

            _cornerAvatar = new AvatarView(64f);
            _cornerAvatar.style.marginRight = 12f;
            corner.Add(_cornerAvatar);
            var texts = Ui.Column();
            texts.pickingMode = PickingMode.Ignore;
            _cornerName = Ui.Text("", 17f, Theme.Text, true);
            _cornerName.pickingMode = PickingMode.Ignore;
            texts.Add(_cornerName);
            _cornerWallet = Ui.Text("", 13f, Theme.Accent);
            _cornerWallet.pickingMode = PickingMode.Ignore;
            texts.Add(_cornerWallet);
            var hint = Ui.Text("Профиль и оформление", 11f, Theme.TextDim);
            hint.pickingMode = PickingMode.Ignore;
            texts.Add(hint);
            corner.Add(texts);

            corner.RegisterCallback<ClickEvent>(_ =>
            {
                _profile.Rebuild();
                ShowPage(Page.Profile);
            });
            corner.RegisterCallback<PointerEnterEvent>(_ => corner.style.backgroundColor = Theme.PanelHover);
            corner.RegisterCallback<PointerLeaveEvent>(_ => corner.style.backgroundColor = Theme.Panel);
            // Аватар Steam приходит не сразу — подтягиваем, пока не появится.
            corner.schedule.Execute(RefreshProfileCorner).Every(1000);
            return corner;
        }

        void RefreshProfileCorner()
        {
            var profile = Profiles.Active;
            _cornerAvatar.Set(profile);
            _cornerName.text = profile.name;
            _cornerWallet.text = $"Кошелёк: {profile.wallet} влияния";
        }

        /// <summary>Счётчик партий пересобирается после сброса статистики.</summary>
        void RebuildStats()
        {
            var old = _pages[Page.Stats];
            int index = _pageHost.IndexOf(old);
            old.RemoveFromHierarchy();
            var fresh = MenuStatsPage.Build(() => ShowPage(Page.Home), () => ShowPage(Page.Stats), RebuildStats);
            _pages[Page.Stats] = fresh;
            _pageHost.Insert(index, fresh);
            ShowPage(Page.Stats);
        }

        static VisualElement PageTitle(string text, Action back)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 18f;
            var backButton = Ui.Button("← Назад", back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            row.Add(backButton);
            row.Add(Ui.Text(text, 34f, Theme.Accent, true));
            return row;
        }

        // ================================================================== Главная

        VisualElement BuildHome(SaveFile saved)
        {
            var page = Ui.Column();
            page.style.alignItems = Align.Center;

            var title = Ui.Text("City Highways", 72f, Theme.Accent, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            page.Add(title);
            var subtitle = Ui.Text("Стройте дороги Москвы, выполняйте контракты мэрии и берите плату за проезд", 17f, Theme.TextDim);
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginTop = 6f;
            subtitle.style.marginBottom = 34f;
            page.Add(subtitle);

            if (saved != null)
            {
                var card = ContinueCard(saved);
                card.style.width = 640f;
                page.Add(card);
            }

            var buttons = Ui.Column();
            buttons.style.width = 420f;
            page.Add(buttons);

            void Add(string text, Action onClick, ButtonKind kind)
            {
                var button = Ui.Button(text, onClick, kind, 21f);
                button.style.height = 54f;
                button.style.marginBottom = 10f;
                buttons.Add(button);
            }
            Add("Новая игра", () => ShowPage(Page.NewGame), saved != null ? ButtonKind.Secondary : ButtonKind.Primary);
            Add("Сетевая игра", () => ShowPage(Page.Online), ButtonKind.Secondary);
            Add("Правила", () => ShowPage(Page.Rules), ButtonKind.Secondary);
            Add("Счётчик партий", () => ShowPage(Page.Stats), ButtonKind.Secondary);
            Add("Настройки", () => ShowPage(Page.Settings), ButtonKind.Secondary);
            if (!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer)
                Add("Выход", Application.Quit, ButtonKind.Ghost);

            var summary = Ui.Text(StatsLog.Summary(), 13f, Theme.TextDim);
            summary.style.marginTop = 10f;
            page.Add(summary);
            return page;
        }

        // ================================================================== Новая игра

        VisualElement BuildNewGame(SaveFile saved, out VisualElement playersList, out UnityEngine.UIElements.Button addPlayer,
            out Label characterHint, out Label piecesValue, out UnityEngine.UIElements.Button hideCards,
            out UnityEngine.UIElements.Button timer, out Label hint)
        {
            var page = Ui.Column();
            page.Add(PageTitle("Новая игра", () => ShowPage(Page.Home)));

            var cards = Ui.Row();
            cards.style.alignItems = Align.FlexStart;
            page.Add(cards);

            var playersCard = Ui.Card(20f);
            playersCard.style.backgroundColor = Theme.Panel;
            playersCard.style.flexGrow = 1f;
            playersCard.style.marginRight = 14f;
            playersCard.Add(Ui.SectionTitle("Игроки"));
            playersList = Ui.Column();
            playersCard.Add(playersList);
            addPlayer = Ui.Button("+ Добавить игрока", AddPlayer, ButtonKind.Ghost, 14f);
            addPlayer.style.marginTop = 6f;
            addPlayer.style.alignSelf = Align.FlexStart;
            playersCard.Add(addPlayer);
            // Способность персонажа, которого выбрали последним.
            characterHint = Ui.Text("", 12f, Theme.TextDim);
            characterHint.style.marginTop = 8f;
            characterHint.style.whiteSpace = WhiteSpace.Normal;
            characterHint.style.maxWidth = 640f;
            playersCard.Add(characterHint);
            cards.Add(playersCard);

            var gameCard = Ui.Card(20f);
            gameCard.style.backgroundColor = Theme.Panel;
            gameCard.style.width = 360f;
            gameCard.Add(Ui.SectionTitle("Параметры партии"));

            var piecesRow = SettingRow("Участков дороги");
            var minus = Ui.Button("–", () => ChangePieces(-1), ButtonKind.Secondary, 18f);
            minus.style.width = 40f;
            piecesRow.Add(minus);
            piecesValue = Ui.Text("", 18f, null, true);
            piecesValue.style.width = 110f;
            piecesValue.style.unityTextAlign = TextAnchor.MiddleCenter;
            piecesRow.Add(piecesValue);
            var plus = Ui.Button("+", () => ChangePieces(1), ButtonKind.Secondary, 18f);
            plus.style.width = 40f;
            piecesRow.Add(plus);
            gameCard.Add(piecesRow);

            var hideRow = SettingRow("Скрывать карты при передаче хода");
            hideCards = Ui.Button("", ToggleHideCards, ButtonKind.Secondary, 14f);
            hideCards.style.width = 120f;
            hideRow.Add(hideCards);
            gameCard.Add(hideRow);

            var timerRow = SettingRow("Таймер хода");
            timer = Ui.Button("", NextTimer, ButtonKind.Secondary, 14f);
            timer.style.width = 120f;
            timerRow.Add(timer);
            gameCard.Add(timerRow);

            hint = Ui.Text("", 13f, Theme.TextDim);
            hint.style.marginTop = 10f;
            hint.style.whiteSpace = WhiteSpace.Normal;
            gameCard.Add(hint);
            cards.Add(gameCard);

            var start = Ui.Button(saved != null ? "Начать новую партию (сохранение удалится)" : "Начать игру", StartGame, ButtonKind.Primary, 22f);
            start.style.marginTop = 20f;
            start.style.height = 58f;
            page.Add(start);
            return page;
        }

        // ================================================================== Настройки

        VisualElement BuildSettings(out UnityEngine.UIElements.Button screenMode, out UnityEngine.UIElements.Button resolution,
            out UnityEngine.UIElements.Button sound, out Label volume)
        {
            var page = Ui.Column();
            page.style.alignItems = Align.Center;
            var title = PageTitle("Настройки", () => ShowPage(Page.Home));
            page.Add(title);

            var card = Ui.Card(24f);
            card.style.backgroundColor = Theme.Panel;
            card.style.width = 620f;

            card.Add(Ui.SectionTitle("Экран"));
            var modeRow = SettingRow("Режим");
            screenMode = Ui.Button("", ToggleScreenMode, ButtonKind.Secondary, 14f);
            screenMode.style.width = 200f;
            modeRow.Add(screenMode);
            card.Add(modeRow);

            var resolutionRow = SettingRow("Разрешение");
            resolution = Ui.Button("", NextResolution, ButtonKind.Secondary, 14f);
            resolution.style.width = 200f;
            resolutionRow.Add(resolution);
            card.Add(resolutionRow);

            var editorNote = Ui.Text("В редакторе режим и разрешение задаёт окно Game — они применятся в собранной игре.", 12f, Theme.TextDim);
            editorNote.style.whiteSpace = WhiteSpace.Normal;
            editorNote.style.marginBottom = 10f;
            Ui.Show(editorNote, Application.isEditor);
            card.Add(editorNote);

            var soundTitle = Ui.SectionTitle("Звук");
            soundTitle.style.marginTop = 12f;
            card.Add(soundTitle);
            var soundRow = SettingRow("Звук");
            sound = Ui.Button("", ToggleSound, ButtonKind.Secondary, 14f);
            sound.style.width = 200f;
            soundRow.Add(sound);
            card.Add(soundRow);

            var volumeRow = SettingRow("Громкость");
            var quieter = Ui.Button("–", () => ChangeVolume(-0.1f), ButtonKind.Secondary, 18f);
            quieter.style.width = 44f;
            volumeRow.Add(quieter);
            volume = Ui.Text("", 18f, null, true);
            volume.style.width = 112f;
            volume.style.unityTextAlign = TextAnchor.MiddleCenter;
            volumeRow.Add(volume);
            var louder = Ui.Button("+", () => ChangeVolume(0.1f), ButtonKind.Secondary, 18f);
            louder.style.width = 44f;
            volumeRow.Add(louder);
            card.Add(volumeRow);

            var tutorialTitle = Ui.SectionTitle("Обучение");
            tutorialTitle.style.marginTop = 12f;
            card.Add(tutorialTitle);
            var tutorialRow = SettingRow("Подсказки на первом ходу");
            var tutorialButton = Ui.Button("Показать снова", () => { }, ButtonKind.Secondary, 14f);
            tutorialButton.style.width = 200f;
            tutorialButton.clicked += () =>
            {
                PlayerPrefs.DeleteKey(GameView.TutorialPrefsKey);
                PlayerPrefs.Save();
                tutorialButton.text = "Покажем в новой партии";
                Ui.SetEnabled(tutorialButton, false);
            };
            tutorialRow.Add(tutorialButton);
            card.Add(tutorialRow);

            page.Add(card);
            return page;
        }

        VisualElement ContinueCard(SaveFile saved)
        {
            var card = Ui.Row();
            card.style.backgroundColor = new Color(0.25f, 0.21f, 0.13f);
            Ui.Radius(card, 10f);
            Ui.Border(card, 2f, Theme.Accent);
            Ui.Padding(card, 14f, 20f);
            card.style.marginBottom = 14f;

            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            texts.Add(Ui.Text("Незаконченная партия", 18f, Theme.Accent, true));
            var names = string.Join(", ", saved.setup.PlayerNames());
            int turn = saved.game != null ? saved.game.turnNumber : 0;
            texts.Add(Ui.Text($"{names} · ход {turn} · сохранена {saved.savedAt}", 14f, Theme.Text));
            card.Add(texts);

            var resume = Ui.Button("Продолжить", () => _app.ContinueGame(saved), ButtonKind.Primary, 18f);
            resume.style.marginRight = 10f;
            card.Add(resume);
            card.Add(Ui.Button("Удалить", () =>
            {
                SaveSystem.Delete();
                _app.ShowMenu();
            }, ButtonKind.Ghost, 14f));
            return card;
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

        /// <summary>Несколько «участков дороги» разных ресурсов по углам — чтобы меню не было пустым.</summary>
        void AddDecorations()
        {
            var resources = new[] { Resource.Asphalt, Resource.Concrete, Resource.Steel, Resource.Gravel, Resource.Machinery, Resource.Asphalt };
            for (int i = 0; i < resources.Length; i++)
            {
                var piece = Ui.Box();
                piece.style.position = Position.Absolute;
                piece.style.width = 70f;
                piece.style.height = 20f;
                piece.style.backgroundColor = Palette.Card(resources[i]);
                piece.style.opacity = 0.25f;
                Ui.Radius(piece, 4f);
                if (i < 3)
                {
                    piece.style.left = 60f + i * 84f;
                    piece.style.top = 50f;
                }
                else
                {
                    piece.style.right = 60f + (i - 3) * 84f;
                    piece.style.bottom = 50f;
                }
                Root.Add(piece);
            }
        }

        void Refresh()
        {
            _playersList.Clear();
            for (int i = 0; i < _setup.players.Count; i++) _playersList.Add(PlayerRow(i));

            Ui.Show(_addPlayerButton, _setup.players.Count < MaxPlayers);
            var config = new GameConfig();
            if (_lastCharacterSlot >= 0 && _lastCharacterSlot < _setup.players.Count && _setup.players[_lastCharacterSlot].character > 0)
            {
                var kind = (CharacterKind)_setup.players[_lastCharacterSlot].character;
                _characterHint.text = $"{Characters.Title(kind)}: {Characters.Ability(kind, config)}";
            }
            else _characterHint.text = "Персонаж: нажмите на кнопку персонажа, чтобы выбрать. «Случайный» раздаёт разных персонажей.";
            _trainsValue.text = _setup.roadPieces > 0 ? _setup.roadPieces.ToString() : "по правилам";
            _hideCardsButton.text = _setup.hideCardsBetweenTurns ? "Да" : "Нет";
            _timerButton.text = _setup.turnSeconds > 0 ? $"{_setup.turnSeconds / 60}:{_setup.turnSeconds % 60:00}" : "Без таймера";
            _soundButton.text = _setup.sound ? "Включён" : "Выключен";
            _screenModeButton.text = _display.fullscreen ? "Полный экран" : "В окне";
            _resolutionButton.text = _display.width > 0 ? $"{_display.width} × {_display.height}" : "Как у монитора";
            _volumeValue.text = Mathf.RoundToInt(_display.volume * 100f) + "%";

            int humans = _setup.players.Count(p => !p.isBot);
            _hint.text = humans == 0
                ? "Все игроки — боты: можно просто смотреть партию."
                : humans == 1
                    ? "Наведите на дорогу — подсказка покажет, хватает ли ресурсов. Умный бот охотно ездит по чужим дорогам — стройте мосты!"
                    : "Игра за одним компьютером: перед ходом карты скрываются, пока игрок не нажмёт «Показать».";
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
            kind.style.width = 170f;
            kind.style.marginRight = 8f;
            row.Add(kind);

            // Персонаж: Случайный → Везунчик → … → Мостовик → Без персонажа → Случайный.
            var character = Ui.Button(CharacterLabel(slot), () =>
            {
                slot.character = slot.character == PlayerSlot.RandomCharacter ? 1
                    : slot.character == 0 ? PlayerSlot.RandomCharacter
                    : slot.character >= (int)CharacterKind.Bridger ? 0
                    : slot.character + 1;
                _lastCharacterSlot = index;
                Refresh();
            }, ButtonKind.Secondary, 14f);
            character.style.width = 190f;
            character.style.marginRight = 8f;
            // Значок рядом с кнопкой (у кнопки-текста свои дети накладывались бы на подпись).
            var icon = new CharacterIcon(26f, slot.character > 0 ? (CharacterKind)slot.character : CharacterKind.None,
                Palette.PlayerColor(index));
            icon.style.marginRight = 6f;
            icon.style.opacity = slot.character > 0 ? 1f : 0.35f;
            row.Add(icon);
            row.Add(character);

            var remove = Ui.Button("×", () =>
            {
                _setup.players.RemoveAt(index);
                Refresh();
            }, ButtonKind.Ghost, 16f);
            remove.style.width = 44f;
            Ui.SetEnabled(remove, _setup.players.Count > MinPlayers);
            row.Add(remove);
            return row;
        }

        static string CharacterLabel(PlayerSlot slot) =>
            slot.character == PlayerSlot.RandomCharacter ? "Персонаж: случайный" : Characters.Title((CharacterKind)slot.character);

        static string KindLabel(PlayerSlot slot) => !slot.isBot ? "Человек" : slot.botLevel == BotLevel.Random ? "Бот: случайный" : "Бот: умный";

        static void CycleKind(PlayerSlot slot)
        {
            // Человек → умный бот → случайный бот → человек.
            if (!slot.isBot)
            {
                slot.isBot = true;
                slot.botLevel = BotLevel.Smart;
            }
            else if (slot.botLevel == BotLevel.Smart) slot.botLevel = BotLevel.Random;
            else slot.isBot = false;
        }

        void AddPlayer()
        {
            if (_setup.players.Count >= MaxPlayers) return;
            var name = GameSetup.DefaultNames.FirstOrDefault(n => _setup.players.All(p => p.name != n))
                       ?? "Игрок " + (_setup.players.Count + 1);
            _setup.players.Add(new PlayerSlot(name, true, BotLevel.Smart));
            Refresh();
        }

        void ChangePieces(int step)
        {
            int current = System.Array.IndexOf(PieceOptions, _setup.roadPieces);
            if (current < 0) current = 0;
            current = Mathf.Clamp(current + step, 0, PieceOptions.Length - 1);
            _setup.roadPieces = PieceOptions[current];
            Refresh();
        }

        void NextTimer()
        {
            var options = GameSetup.TurnTimerOptions;
            int index = System.Array.IndexOf(options, _setup.turnSeconds);
            _setup.turnSeconds = options[(index + 1) % options.Length];
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
            _setup.Save();
            Refresh();
        }

        void ToggleScreenMode()
        {
            _display.fullscreen = !_display.fullscreen;
            ApplyDisplay();
        }

        void NextResolution()
        {
            int current = _resolutions.FindIndex(r => r.x == _display.width && r.y == _display.height);
            var next = _resolutions[(current + 1) % _resolutions.Count];
            _display.width = next.x;
            _display.height = next.y;
            ApplyDisplay();
        }

        void ChangeVolume(float delta)
        {
            _display.volume = Mathf.Clamp01(Mathf.Round((_display.volume + delta) * 10f) / 10f);
            ApplyDisplay();
            _app.Sound.Play(Sfx.Bell, 0.6f);
        }

        void ApplyDisplay()
        {
            _display.Save();
            _display.Apply();
            Refresh();
        }

        void StartGame() => _app.StartGame(_setup);
    }
}
