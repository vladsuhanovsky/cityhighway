using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using CityHighways.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Главное меню из страниц: главная (кнопки), «Новая игра» (игроки и параметры партии),
    /// «Обучение», «Правила» и «Счётчик партий» (каждая — по трём режимам: магнат, соревнование, кооператив)
    /// и «Настройки» (экран и звук).
    /// </summary>
    public sealed partial class MainMenuView
    {
        const int MinPlayers = 2;
        const int MaxPlayers = 5;
        /// <summary>Варианты числа участков: 0 — «по правилам» (зависит от числа игроков).</summary>
        static readonly int[] PieceOptions = { 0, 15, 20, 25, 30, 35, 40 };

        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        readonly DisplaySettings _display;

        readonly VisualElement _playersList;
        readonly UnityEngine.UIElements.Button _addPlayerButton;
        readonly Label _characterHint;
        int _lastCharacterSlot = -1;
        // Строки параметров, которых нет в «Логистическом магнате» (прячутся при выборе режима).
        VisualElement _scenarioRow, _piecesRow, _hideRow, _timerRow, _difficultyRow, _mapRow;
        const float NewGameCardsHeight = 530f;
        UnityEngine.UIElements.Button _difficultyButton;
        Label _difficultyHint;
        readonly Label _trainsValue;
        readonly UnityEngine.UIElements.Button _hideCardsButton;
        readonly UnityEngine.UIElements.Button _timerButton;
        readonly MenuSettingsPage _settings;
        readonly Label _hint;
        readonly VisualElement _overlay;

        enum Page { Home, NewGame, Online, Profile, Rules, Stats, Settings, Tutorial }
        readonly Dictionary<Page, VisualElement> _pages = new Dictionary<Page, VisualElement>();
        readonly VisualElement _pageHost;
        readonly MenuOnlinePage _online;
        readonly MenuProfilePage _profile;
        readonly RulesView _rules;
        /// <summary>Куда ведёт «Назад» со страницы правил: на главную или в «Обучение» (если открыли оттуда).</summary>
        Page _rulesReturn = Page.Home;

        public VisualElement Root { get; }

        public MainMenuView(CityHighwaysApp app, GameSetup setup)
        {
            _app = app;
            _setup = setup;
            _display = app.Display ?? DisplaySettings.Load();

            Root = new VisualElement();
            Root.style.flexGrow = 1f;
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            Root.style.backgroundColor = Theme.Background;

            // Живой город на заднем плане: параллакс за мышью, день и ночь.
            _background = new MenuBackground
            {
                Mode = (MenuBackground.LightMode)Mathf.Clamp(_display.menuLight, 0, 2),
                PaletteIndex = Mathf.Clamp(_display.menuPalette, 0, MenuBackground.PaletteNames.Length - 1)
            };
            Root.Add(_background);
            BuildKeyArt();
            Root.RegisterCallback<PointerMoveEvent>(e =>
            {
                var size = Root.layout.size;
                if (size.x < 1f || size.y < 1f) return;
                var local = Root.WorldToLocal(e.position);
                var pointer = new Vector2(local.x / size.x, local.y / size.y);
                _background.SetPointer(pointer);
                // Картина города слегка сдвигается за мышью — тот же параллакс, что у живого фона.
                if (_keyArtImage != null)
                    _keyArtImage.style.translate = new Translate(new Length((0.5f - pointer.x) * 18f), new Length((0.5f - pointer.y) * 10f));
            });
            MenuButton.Hovered = () => { if (_display.uiSounds) _app.Sound.Play(Sfx.Tick, 0.35f); };
            Profiles.SyncSteam();
            // С сервера пришли изменения (начисление, вещь, бан) — обновить уголок профиля и уведомления.
            CloudProfiles.Changed += () =>
            {
                if (Root.panel == null) return;
                RefreshProfileCorner();
                RefreshNotifications();
            };

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
            _rules = MenuRulesPage.Create(new GameConfig(), () => ShowPage(_rulesReturn));
            _pages[Page.Rules] = _rules.Root;
            _pages[Page.Tutorial] = BuildTutorial();
            _pages[Page.Stats] = MenuStatsPage.Build(() => ShowPage(Page.Home), () => ShowPage(Page.Stats), RebuildStats);
            _settings = new MenuSettingsPage(app, _setup, _display, () => ShowPage(Page.Home), ApplyBackgroundSettings);
            _pages[Page.Settings] = _settings.Root;
            foreach (var page in _pages.Values) _pageHost.Add(page);

            _overlay = new VisualElement();
            Ui.Fill(_overlay);
            _overlay.style.backgroundColor = Theme.Shade;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            Ui.Show(_overlay, false);

            _profileCorner = BuildProfileCorner();
            Root.Add(_profileCorner);
            _topBar = BuildTopBar();
            Root.Add(_topBar);
            _bellPanel = BuildBellPanel();
            Root.Add(_bellPanel);
            Root.Add(_overlay);

            ShowPage(Page.Home);
            Refresh();
        }

        /// <summary>Друг пригласил нас в лобби Steam — открываем «Сетевую игру» и входим.</summary>
        public void JoinSteamLobby(ulong lobby)
        {
            if (Profiles.Active.cloudBanned) return;
            ShowPage(Page.Online);
            _online.JoinSteam(lobby);
        }

        void ShowPage(Page page)
        {
            _page = page;
            foreach (var pair in _pages) Ui.Show(pair.Value, pair.Key == page);
            // Единый переход: страница проявляется и чуть поднимается.
            if (page != Page.Home && _pages.TryGetValue(page, out var shown)) Ui.Appear(shown);
            if (page == Page.Settings) _settings.Rebuild();
            if (page == Page.Tutorial) RefreshTutorial();
            // На страницах с текстом фон приглушается, верхние значки и уведомления — только на главной.
            _background.Dim = page == Page.Home ? 0f : 1f;
            if (_topBar != null) Ui.Show(_topBar, page == Page.Home);
            if (_bellOpen) ToggleBell();
            if (page == Page.Home)
            {
                RefreshNotifications();
                RefreshOnlineBadge();
            }
            if (_profileCorner != null)
            {
                // Аватар в углу — на главной; на остальных страницах он только мешал бы.
                Ui.Show(_profileCorner, page == Page.Home);
                if (page == Page.Home) RefreshProfileCorner();
            }
        }

        VisualElement _profileCorner;
        ProfileCard _profileCard;

        /// <summary>Карточка профиля в левом нижнем углу главного меню: клик — полный профиль.</summary>
        VisualElement BuildProfileCorner()
        {
            _profileCard = new ProfileCard(() =>
            {
                _profile.Rebuild();
                ShowPage(Page.Profile);
            });
            _profileCard.style.position = Position.Absolute;
            _profileCard.style.left = 28f;
            _profileCard.style.bottom = 28f;
            // Аватар Steam приходит не сразу — подтягиваем, пока не появится.
            _profileCard.schedule.Execute(RefreshProfileCorner).Every(1000);
            return _profileCard;
        }

        void RefreshProfileCorner() => _profileCard?.Set(Profiles.Active);

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
            row.Add(Ui.Text(text, 34f, Theme.Gold, true));
            return row;
        }

        // ================================================================== Главная

        VisualElement BuildHome(SaveFile saved)
        {
            var page = Ui.Column();
            page.style.alignItems = Align.Center;

            // Заголовок между двумя силуэтами города.
            var titleRow = Ui.Row();
            titleRow.style.alignItems = Align.Center;
            var leftCity = new MenuIcon(MenuIcon.Glyph.Skyline, 52f, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.85f));
            leftCity.style.marginRight = 18f;
            titleRow.Add(leftCity);
            var title = Ui.Text("City Highways", 72f, Theme.Gold, true);
            Fonts.Apply(title, Fonts.Weight.Logo);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            titleRow.Add(title);
            var rightCity = new MenuIcon(MenuIcon.Glyph.Skyline, 52f, new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.85f));
            rightCity.style.marginLeft = 18f;
            titleRow.Add(rightCity);
            page.Add(titleRow);
            var subtitle = Ui.Text("Стройте дороги, выполняйте контракты мэрии и берите плату за проезд", 17f, Theme.Text);
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginTop = 6f;
            subtitle.style.marginBottom = 18f;
            page.Add(subtitle);

            // Новости: что появилось в игре, меняются каждые несколько секунд.
            _newsBox = Ui.Row();
            _newsBox.style.width = 780f;
            _newsBox.style.alignSelf = Align.Center;
            _newsBox.style.alignItems = Align.Center;
            _newsBox.style.minHeight = 48f;
            _newsBox.style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(_newsBox, 12f);
            Ui.Border(_newsBox, 1f, Theme.Line);
            Ui.Padding(_newsBox, 10f, 14f);
            _newsBox.style.marginBottom = 18f;
            Ui.Shadow(_newsBox, 12f);
            var newsIcon = new MenuIcon(MenuIcon.Glyph.News, 22f, Theme.Gold);
            newsIcon.style.flexShrink = 0f;
            _newsBox.Add(newsIcon);
            var newsCaption = Ui.Text("Новости:", 15f, Theme.Gold, true);
            newsCaption.style.marginLeft = 10f;
            newsCaption.style.marginRight = 8f;
            // Подпись не сжимается и не переносится: иначе «Новости» рвётся на «Новост / и:».
            newsCaption.style.flexShrink = 0f;
            newsCaption.style.whiteSpace = WhiteSpace.NoWrap;
            _newsBox.Add(newsCaption);
            _newsText = Ui.Text(News[0], 15f, Theme.Text);
            _newsText.style.flexShrink = 1f;
            _newsText.style.flexGrow = 1f;
            _newsText.style.whiteSpace = WhiteSpace.Normal;
            Ui.Transitions(_newsText, "opacity");
            _newsBox.Add(_newsText);
            page.Add(_newsBox);

            if (saved != null)
            {
                var card = ContinueCard(saved);
                card.style.width = 560f;
                page.Add(card);
            }

            var buttons = Ui.Column();
            buttons.style.width = 480f;
            page.Add(buttons);

            MenuButton Add(MenuIcon.Glyph glyph, string text, Action onClick, bool primary)
            {
                var button = new MenuButton(glyph, text, onClick, primary);
                _menuButtons.Add(button);
                buttons.Add(button);
                return button;
            }
            var newGame = Add(MenuIcon.Glyph.Gamepad, "Новая игра", () => ShowPage(Page.NewGame), saved == null);
            // «Быстрый старт» — сразу в партию с последними настройками.
            var quick = Ui.Button("Быстрый старт", StartGame, ButtonKind.Secondary, 13f);
            quick.style.minHeight = 30f;
            Ui.Padding(quick, 4f, 12f);
            quick.style.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
            newGame.Badge.Add(quick);
            Add(MenuIcon.Glyph.Cap, AnyTutorialDone ? "Обучение" : "Обучение — начните отсюда", () => ShowPage(Page.Tutorial), false);
            _onlineButton = Add(MenuIcon.Glyph.Globe, "Сетевая игра", () =>
            {
                // Заблокированному аккаунту сетевая игра закрыта — показываем причину в уведомлениях.
                if (Profiles.Active.cloudBanned)
                {
                    if (!_bellOpen) ToggleBell();
                    return;
                }
                ShowPage(Page.Online);
            }, false);
            _onlineBadge = Pill("");
            Ui.Show(_onlineBadge, false);
            _onlineButton.Badge.Add(_onlineBadge);
            Add(MenuIcon.Glyph.Book, "Правила", () => OpenRules(null, Page.Home), false);
            Add(MenuIcon.Glyph.Chart, "Счётчик партий", () => ShowPage(Page.Stats), false);
            Add(MenuIcon.Glyph.Gear, "Настройки", () => ShowPage(Page.Settings), false);

            var summary = Ui.Text(StatsLog.Summary(), 14f, Theme.Text);
            summary.style.marginTop = 8f;
            page.Add(summary);
            return page;
        }

        /// <summary>Маленькая плашка-значок (счётчик «онлайн», «новое»).</summary>
        static Label Pill(string text)
        {
            var pill = Ui.Text(text, 12f, Color.white, true);
            pill.style.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
            pill.style.whiteSpace = WhiteSpace.NoWrap;
            Ui.Radius(pill, 10f);
            Ui.Padding(pill, 3f, 9f);
            return pill;
        }

        // ------------------------------------------------------------------ Новости

        /// <summary>Что нового в игре — короткие строки для ленты на главной.</summary>
        static readonly string[] News =
        {
            "Магнат: хабы можно перехватить, отстающие ходят первыми, свои дороги быстрее, равный старт, второму в заказе мэрии — половина",
            "Магнат: первый раунд — раунд стройки. Каждая компания строит по дороге, фуры берут грузы со второго раунда",
            "Построенные дороги обрамлены цветом компании, а не белым — сразу видно, чья трасса",
            "В гардеробе — описания вещей и подсказка, что под чем не видно: например, галстук под свитером",
            "Гардероб: шляпы больше не путаются с причёсками, комбинезон с нагрудником поверх футболки, одежда не «висит» при надевании",
            "Древо мастерства персонажей в профиле, мосты Мостовика подсвечены его цветом, аватары в выборе персонажа «выпрыгивают»",
            "Дороги отмечены цветом владельца, в соревновании появились «Слои карты», Москва без МКАД, река через всю карту и ровные мосты",
            "Профиль: контейнеры за влияние — одежда, эмблемы, титулы и рамки, восемь вещей бывают только в контейнерах",
            "Магнат: за опасный груз — новая фура (до 5). Плавное вращение поля, раздел «Управление» в настройках, Esc — назад в меню",
            "Магнат: хабы встают в стороне от дорог и рек, на ровной земле — без серой плиты, с подъездом от станции",
            "Профиль: персонаж без розовой каймы и мерцания, свиток и планшет держатся в руке, шляпы и одежда садятся ровно",
            "3D-город живее: пешеходы у станций и хабов, светофоры на перекрёстках, рощи на холмах, дворы и техника на крышах, поезда и ангары у крупных хабов",
            "3D-рельеф: холмы и речные долины, дороги по склонам с подрезками и насыпями, у каждой карты свой рельеф",
            "3D: дороги с бордюрами цвета компании, павильоны станций, машины и автобусы на улицах, 3D-фуры в магнате",
            "3D-камера: крутите и наклоняйте карту правой кнопкой мыши (Q/E, R/F), компас под мини-картой; фонари, кусты и лодки в городе",
            "Магнат: «Игроки», «Грузы» и «Заказы мэрии» — панелями справа, тонкий пунктир дорог, 3D-терминалы, краны и фуры у хабов",
            "Магнат: пунктир для новых дорог, вокзалы и аэропорты в 3D, грузы справа на карте и учебная карта в «Обучении»",
            "Экран партии: «Игроки» по центру внизу с портретами из профиля, ресурсы рядом, журнал — наверху у меню",
            "Новые портреты персонажей и свой звук у каждого: монетка, молоток, портфель, касса, печать, балка",
            "Новая игра: карточка выбора персонажа — герой, сложность, способность и ваша статистика побед",
            "Профиль: мужской и женский персонаж, новый гардероб из 58 вещей в стиле 3D-героев",
            "Персонажи получили лица: 3D-герои Везунчика, Прораба, Мостовика и остальных",
            "Магнат: цены дорог прямо на карте, жребий первого хода, правила и журнал вверху",
            "Новые иконки грузов, хабов и событий, картина города в меню и иллюстрации карточек",
            "Новый интерфейс партии: карта на весь экран, мини-карта и карточки контрактов",
            "Поле с наклоном 2.5D: утро, день, закат и ночь в одной партии",
            "Сценарии: Зима, Большой город, Год ремонта и Выборы мэра",
            "Новые карты: Амстердам с каналами и Чунцин — город мостов на двух реках",
            "Новый режим: кооператив «Город против всех» — одна команда против кризиса",
            "Обучающая партия с наставником — лучший способ начать",
            "Пробки и пропуска: объезды выгоднее, магистрали не стоят в пробках",
            "Достижения и задание дня приносят влияние на гардероб персонажа"
        };

        /// <summary>Номер выпуска новостей: если больше прочитанного — в уведомлениях «Что нового».</summary>
        const int NewsVersion = 52;
        const string NewsSeenKey = "CityHighways.NewsSeen";

        int _newsIndex;
        float _newsTimer;

        void TickNews(float dt)
        {
            if (_newsText == null) return;
            _newsTimer += dt;
            if (_newsTimer < 7f) return;
            _newsTimer = 0f;
            _newsIndex = (_newsIndex + 1) % News.Length;
            int index = _newsIndex;
            _newsText.style.opacity = 0f;
            _newsText.schedule.Execute(() =>
            {
                _newsText.text = News[index];
                _newsText.style.opacity = 1f;
            }).ExecuteLater(320);
        }

        // ------------------------------------------------------------------ Верхняя панель

        VisualElement _topBar;
        VisualElement _bellPanel;
        VisualElement _bellList;
        Label _bellCount;
        bool _bellOpen;
        Label _lightCaption;
        VisualElement _moonButton, _sunButton;

        /// <summary>Круглая кнопка-значок в верхней панели меню.</summary>
        VisualElement TopIcon(MenuIcon.Glyph glyph, Action onClick)
        {
            var button = Ui.Box();
            button.pickingMode = PickingMode.Position;
            button.style.width = 42f;
            button.style.height = 42f;
            button.style.marginRight = 8f;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(button, 21f);
            Ui.Border(button, 1f, Theme.Line);
            Ui.Transitions(button, "background-color", "translate", "border-top-color", "border-bottom-color", "border-left-color", "border-right-color");
            button.Add(new MenuIcon(glyph, 20f, Theme.Text));
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                button.style.backgroundColor = Theme.PanelHover;
                button.style.translate = new Translate(new Length(0f), new Length(-2f));
                MenuButton.Hovered?.Invoke();
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                button.style.backgroundColor = Theme.PanelGlass;
                button.style.translate = new Translate(new Length(0f), new Length(0f));
            });
            button.RegisterCallback<ClickEvent>(_ =>
            {
                Ui.ButtonClicked?.Invoke();
                onClick?.Invoke();
            });
            return button;
        }

        VisualElement BuildTopBar()
        {
            var bar = Ui.Row();
            bar.style.position = Position.Absolute;
            bar.style.left = 24f;
            bar.style.right = 24f;
            bar.style.top = 18f;
            bar.style.justifyContent = Justify.SpaceBetween;

            // Слева — свет фона: ночь, день, палитра.
            var left = Ui.Row();
            _moonButton = TopIcon(MenuIcon.Glyph.Moon, () => SetLight(_background.Mode == MenuBackground.LightMode.Night
                ? MenuBackground.LightMode.Auto : MenuBackground.LightMode.Night));
            _sunButton = TopIcon(MenuIcon.Glyph.Sun, () => SetLight(_background.Mode == MenuBackground.LightMode.Day
                ? MenuBackground.LightMode.Auto : MenuBackground.LightMode.Day));
            left.Add(_moonButton);
            left.Add(_sunButton);
            left.Add(TopIcon(MenuIcon.Glyph.Palette, NextPalette));
            _lightCaption = Ui.Text("", 13f, Theme.Text);
            _lightCaption.style.marginLeft = 4f;
            _lightCaption.style.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            Ui.Radius(_lightCaption, 8f);
            Ui.Padding(_lightCaption, 4f, 10f);
            left.Add(_lightCaption);
            bar.Add(left);

            // Справа — уведомления, настройки, выход.
            var right = Ui.Row();
            var bell = TopIcon(MenuIcon.Glyph.Bell, ToggleBell);
            _bellCount = Ui.Number("", 11f, Color.white, true);
            _bellCount.style.position = Position.Absolute;
            _bellCount.style.top = -4f;
            _bellCount.style.right = -4f;
            _bellCount.style.minWidth = 18f;
            _bellCount.style.height = 18f;
            _bellCount.style.unityTextAlign = TextAnchor.MiddleCenter;
            _bellCount.style.backgroundColor = Theme.Accent;
            Ui.Radius(_bellCount, 9f);
            Ui.Padding(_bellCount, 0f, 4f);
            bell.Add(_bellCount);
            right.Add(bell);
            right.Add(TopIcon(MenuIcon.Glyph.Gear, () => ShowPage(Page.Settings)));
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                var quit = TopIcon(MenuIcon.Glyph.Close, ConfirmQuit);
                quit.style.marginRight = 0f;
                right.Add(quit);
            }
            bar.Add(right);
            RefreshLightButtons();
            return bar;
        }

        /// <summary>Свет и палитра фона поменялись в «Настройках» (или сброшены) — применить к живому фону.</summary>
        void ApplyBackgroundSettings()
        {
            _background.Mode = (MenuBackground.LightMode)Mathf.Clamp(_display.menuLight, 0, 2);
            _background.PaletteIndex = Mathf.Clamp(_display.menuPalette, 0, MenuBackground.PaletteNames.Length - 1);
            ApplyKeyArt();
            RefreshLightButtons();
        }

        VisualElement _keyArt, _keyArtImage;

        /// <summary>
        /// Картина города из Scenario (Resources/CityHighways/UI/key_art) поверх живого фона: на весь экран,
        /// с лёгким затемнением, чтобы читались кнопки. Нет картинки или выключена в настройках — живой фон.
        /// </summary>
        void BuildKeyArt()
        {
            var texture = GeneratedArt.Load(GeneratedArt.KeyArt);
            if (texture == null) return;
            _keyArt = new VisualElement();
            Ui.Fill(_keyArt);
            _keyArt.style.overflow = Overflow.Hidden;
            _keyArt.pickingMode = PickingMode.Ignore;
            _keyArtImage = new VisualElement();
            _keyArtImage.style.position = Position.Absolute;
            _keyArtImage.style.left = -24f;
            _keyArtImage.style.right = -24f;
            _keyArtImage.style.top = -16f;
            _keyArtImage.style.bottom = -16f;
            _keyArtImage.style.backgroundImage = texture;
            _keyArtImage.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            _keyArtImage.pickingMode = PickingMode.Ignore;
            Ui.Transitions(_keyArtImage, "translate");
            _keyArt.Add(_keyArtImage);
            var shade = new VisualElement();
            Ui.Fill(shade);
            shade.style.backgroundColor = new Color(0.03f, 0.04f, 0.08f, 0.38f);
            shade.pickingMode = PickingMode.Ignore;
            _keyArt.Add(shade);
            Root.Insert(Root.IndexOf(_background) + 1, _keyArt);
            ApplyKeyArt();
        }

        void ApplyKeyArt()
        {
            if (_keyArt == null) return;
            bool on = _display.menuKeyArt;
            Ui.Show(_keyArt, on);
            Ui.Show(_background, !on);
        }

        void SetLight(MenuBackground.LightMode mode)
        {
            _background.Mode = mode;
            _display.menuLight = (int)mode;
            _display.Save();
            RefreshLightButtons();
        }

        void NextPalette()
        {
            _background.PaletteIndex = (_background.PaletteIndex + 1) % MenuBackground.PaletteNames.Length;
            _display.menuPalette = _background.PaletteIndex;
            _display.Save();
            RefreshLightButtons();
        }

        void RefreshLightButtons()
        {
            var mode = _background.Mode;
            Ui.Border(_moonButton, mode == MenuBackground.LightMode.Night ? 2f : 1f, mode == MenuBackground.LightMode.Night ? Theme.Accent : Theme.Line);
            Ui.Border(_sunButton, mode == MenuBackground.LightMode.Day ? 2f : 1f, mode == MenuBackground.LightMode.Day ? Theme.Accent : Theme.Line);
            string light = mode == MenuBackground.LightMode.Night ? "ночь" : mode == MenuBackground.LightMode.Day ? "день" : "по часам";
            _lightCaption.text = $"Свет: {light} · {MenuBackground.PaletteNames[_background.PaletteIndex]}";
        }

        void ConfirmQuit()
        {
            _overlay.Clear();
            var box = Ui.Card(24f);
            box.pickingMode = PickingMode.Position;
            box.style.width = 420f;
            box.style.backgroundColor = Theme.Panel;
            Ui.Shadow(box, 10f, 20f, 0.6f);
            box.Add(Ui.Text("Выйти из City Highways?", 22f, Theme.Text, true));
            var note = Ui.Text("Незаконченная партия сохранится — её можно будет продолжить.", 14f, Theme.TextDim);
            note.style.marginTop = 6f;
            note.style.marginBottom = 16f;
            box.Add(note);
            var row = Ui.Row();
            var quit = Ui.Button("Выйти", Quit, ButtonKind.Primary, 15f);
            quit.style.marginRight = 10f;
            row.Add(quit);
            row.Add(Ui.Button("Остаться", () => Ui.Show(_overlay, false), ButtonKind.Secondary, 15f));
            box.Add(row);
            _overlay.Add(box);
            Ui.Show(_overlay, true);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ Уведомления

        VisualElement BuildBellPanel()
        {
            var panel = Ui.Column();
            panel.pickingMode = PickingMode.Position;
            panel.style.position = Position.Absolute;
            panel.style.top = 70f;
            panel.style.right = 24f;
            panel.style.width = 380f;
            panel.style.backgroundColor = Theme.Panel;
            Ui.Radius(panel, 14f);
            Ui.Border(panel, 1f, Theme.Line);
            Ui.Padding(panel, 12f, 14f);
            Ui.Shadow(panel, 14f, 18f, 0.55f);
            panel.Add(Ui.Text("Уведомления", 18f, Theme.Text, true));
            _bellList = Ui.Column();
            _bellList.style.marginTop = 8f;
            panel.Add(_bellList);
            panel.style.display = DisplayStyle.None;
            panel.style.opacity = 0f;
            return panel;
        }

        void ToggleBell()
        {
            _bellOpen = !_bellOpen;
            if (_bellOpen) RefreshNotifications();
            Ui.Slide(_bellPanel, _bellOpen, 0f, -6f);
            _app.Sound.Play(_bellOpen ? Sfx.Open : Sfx.Close, 0.45f);
        }

        /// <summary>Что стоит сделать: задание дня, покупка на накопленное влияние, незаконченная партия, новости, обучение.</summary>
        List<(MenuIcon.Glyph glyph, string text, Action action)> Notifications()
        {
            var list = new List<(MenuIcon.Glyph, string, Action)>();
            var profile = Profiles.Active;
            if (profile != null && profile.cloudBanned)
                list.Add((MenuIcon.Glyph.Flag, "Аккаунт заблокирован" + (string.IsNullOrWhiteSpace(profile.cloudBanReason) ? "" : ": " + profile.cloudBanReason) +
                          ". Сетевая игра недоступна.", null));
            foreach (var message in CloudProfiles.Messages.ToList())
            {
                string shown = message;
                list.Add((MenuIcon.Glyph.Envelope, shown, () => { CloudProfiles.Messages.Remove(shown); RefreshNotifications(); }));
            }
            void OpenProfile()
            {
                _profile.Rebuild();
                ShowPage(Page.Profile);
            }
            if (profile != null && profile.dailyDone != Achievements.DateKey(DateTime.Now))
                list.Add((MenuIcon.Glyph.Star, $"Задание дня: {Achievements.Today.Title} (+{Achievements.DailyReward} влияния)", OpenProfile));
            if (profile != null)
            {
                var item = Profiles.Catalog.Where(i => !i.Exclusive && !profile.Owns(i.Id) && i.Price > 0 && i.Price <= profile.wallet)
                    .OrderByDescending(i => i.Price).FirstOrDefault();
                if (item != null) list.Add((MenuIcon.Glyph.Palette, $"Хватает влияния на «{item.Name}» ({item.Price}) — загляните в профиль", OpenProfile));
            }
            var saved = SaveSystem.Load();
            if (saved != null) list.Add((MenuIcon.Glyph.Gamepad, "Есть незаконченная партия — можно продолжить", () => _app.ContinueGame(saved)));
            if (PlayerPrefs.GetInt(NewsSeenKey, 0) < NewsVersion)
                list.Add((MenuIcon.Glyph.News, "Что нового: " + News[0], () =>
                {
                    PlayerPrefs.SetInt(NewsSeenKey, NewsVersion);
                    PlayerPrefs.Save();
                    RefreshNotifications();
                }));
            if (!AnyTutorialDone) list.Add((MenuIcon.Glyph.Cap, "Обучение ещё не пройдено — выберите режим и начните с наставником", () => ShowPage(Page.Tutorial)));
            return list;
        }

        void RefreshNotifications()
        {
            if (_bellList == null) return;
            var list = Notifications();
            _bellCount.text = list.Count.ToString();
            Ui.Show(_bellCount, list.Count > 0);
            _bellList.Clear();
            if (list.Count == 0)
            {
                _bellList.Add(Ui.Text("Новых уведомлений нет.", 14f, Theme.TextDim));
                return;
            }
            foreach (var (glyph, text, action) in list)
            {
                var row = Ui.Row();
                row.pickingMode = PickingMode.Position;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6f;
                row.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(row, 10f);
                Ui.Padding(row, 8f, 10f);
                Ui.Transitions(row, "background-color");
                var icon = new MenuIcon(glyph, 20f, Theme.Gold);
                icon.style.marginRight = 10f;
                row.Add(icon);
                var label = Ui.Text(text, 14f, Theme.Text);
                label.style.flexShrink = 1f;
                row.Add(label);
                var act = action;
                row.RegisterCallback<PointerEnterEvent>(_ => row.style.backgroundColor = Theme.PanelHover);
                row.RegisterCallback<PointerLeaveEvent>(_ => row.style.backgroundColor = Theme.PanelLight);
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    Ui.ButtonClicked?.Invoke();
                    if (_bellOpen) ToggleBell();
                    act?.Invoke();
                });
                _bellList.Add(row);
            }
        }

        // ------------------------------------------------------------------ «Онлайн»

        MenuButton _onlineButton;
        Label _onlineBadge;
        float _onlineTimer;

        /// <summary>
        /// Плашка у «Сетевой игры»: сколько открытых игр в Unity Gaming Services и сколько друзей Steam в лобби.
        /// Только настоящие числа: нет сервиса или ничего не нашлось — плашки нет.
        /// </summary>
        void RefreshOnlineBadge()
        {
            if (_onlineBadge == null) return;
            int friends = 0;
            try
            {
                var steam = NetPlatforms.Steam;
                if (steam != null && steam.Available) friends = steam.FriendsInLobbies()?.Count ?? 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Steam: не удалось узнать друзей в лобби: " + e.Message);
            }
            var ugs = NetPlatforms.Ugs;
            if (ugs == null)
            {
                SetOnlineBadge(-1, friends);
                return;
            }
            ugs.QueryLobbies((lobbies, error) => SetOnlineBadge(error == null && lobbies != null ? lobbies.Count : -1, friends), true);
        }

        void SetOnlineBadge(int open, int friends)
        {
            var parts = new List<string>();
            if (open > 0) parts.Add($"{open} {Plural(open, "открытая игра", "открытые игры", "открытых игр")}");
            if (friends > 0) parts.Add($"друзей в лобби: {friends}");
            _onlineBadge.text = string.Join(" · ", parts);
            Ui.Show(_onlineBadge, parts.Count > 0);
        }

        static string Plural(int n, string one, string few, string many)
        {
            int last = n % 10, lastTwo = n % 100;
            if (last == 1 && lastTwo != 11) return one;
            if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) return few;
            return many;
        }

        // ------------------------------------------------------------------ Кадр

        MenuBackground _background;
        readonly List<MenuButton> _menuButtons = new List<MenuButton>();
        VisualElement _newsBox;
        Label _newsText;
        Page _page = Page.Home;

        /// <summary>Анимация меню: фон, блики на кнопках, рамка профиля, лента новостей, плашка «онлайн».</summary>
        public void Tick(float dt)
        {
            // Esc — в предыдущий раздел (самая вложенная видимая кнопка «← Назад»).
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Ui.GoBack(Root);
            _background.Tick(dt);
            if (_page == Page.Profile) _profile.Tick(dt);
            if (_page != Page.Home) return;
            foreach (var button in _menuButtons) button.Tick(dt);
            _profileCard?.Tick(dt);
            TickNews(dt);
            _onlineTimer += dt;
            if (_onlineTimer > 30f)
            {
                _onlineTimer = 0f;
                RefreshOnlineBadge();
            }
        }

        // ================================================================== Новая игра

        VisualElement BuildNewGame(SaveFile saved, out VisualElement playersList, out UnityEngine.UIElements.Button addPlayer,
            out Label characterHint, out Label piecesValue, out UnityEngine.UIElements.Button hideCards,
            out UnityEngine.UIElements.Button timer, out Label hint)
        {
            var page = Ui.Column();
            page.Add(PageTitle("Новая игра", () => ShowPage(Page.Home)));

            // Высота карточек постоянная (по самому длинному режиму — соревнованию): при смене режима и числа
            // игроков страница не прыгает вверх-вниз (она отцентрована по высоте экрана).
            var cards = Ui.Row();
            cards.style.alignItems = Align.Stretch;
            cards.style.height = NewGameCardsHeight;
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

            var modeRow = SettingRow("Режим");
            // Режимы по кругу: Магнат → Соревнование → Кооператив.
            _modeButton = Ui.Button("", () =>
            {
                if (_setup.magnate) { _setup.magnate = false; _setup.cooperative = false; }
                else if (!_setup.cooperative) _setup.cooperative = true;
                else { _setup.cooperative = false; _setup.magnate = true; }
                Refresh();
            }, ButtonKind.Secondary, 14f);
            _modeButton.style.width = 170f;
            modeRow.Add(_modeButton);
            gameCard.Add(modeRow);
            _modeHint = Hint();
            gameCard.Add(_modeHint);

            // Карта — под режимом: от режима зависит, есть ли выбор карты (в магнате город генерируется), и при
            // переключении режимов строка «Режим» остаётся на месте.
            var mapRow = SettingRow("Карта");
            _mapRow = mapRow;
            _mapButton = Ui.Button("", NextMap, ButtonKind.Secondary, 14f);
            _mapButton.style.width = 170f;
            mapRow.Add(_mapButton);
            gameCard.Add(mapRow);
            _mapHint = Hint();
            gameCard.Add(_mapHint);

            var scenarioRow = SettingRow("Сценарий");
            _scenarioButton = Ui.Button("", NextScenario, ButtonKind.Secondary, 14f);
            _scenarioButton.style.width = 170f;
            scenarioRow.Add(_scenarioButton);
            gameCard.Add(scenarioRow);
            _scenarioRow = scenarioRow;
            _scenarioHint = Ui.Text("", 12f, Theme.TextDim);
            _scenarioHint.style.whiteSpace = WhiteSpace.Normal;
            _scenarioHint.style.marginTop = -4f;
            _scenarioHint.style.marginBottom = 12f;
            gameCard.Add(_scenarioHint);

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
            _piecesRow = piecesRow;

            var hideRow = SettingRow("Скрывать карты при передаче хода");
            hideCards = Ui.Button("", ToggleHideCards, ButtonKind.Secondary, 14f);
            hideCards.style.width = 120f;
            hideRow.Add(hideCards);
            gameCard.Add(hideRow);
            _hideRow = hideRow;

            var timerRow = SettingRow("Таймер хода");
            timer = Ui.Button("", NextTimer, ButtonKind.Secondary, 14f);
            timer.style.width = 120f;
            timerRow.Add(timer);
            gameCard.Add(timerRow);
            _timerRow = timerRow;

            // Сложность магната: частота грузов, награды, события, штрафы за пробки и стартовые очки стройки.
            var difficultyRow = SettingRow("Сложность");
            _difficultyButton = Ui.Button("", NextDifficulty, ButtonKind.Secondary, 14f);
            _difficultyButton.style.width = 150f;
            difficultyRow.Add(_difficultyButton);
            gameCard.Add(difficultyRow);
            _difficultyRow = difficultyRow;
            _difficultyHint = Ui.Text("", 12f, Theme.TextDim);
            _difficultyHint.style.whiteSpace = WhiteSpace.Normal;
            _difficultyHint.style.marginTop = -4f;
            _difficultyHint.style.marginBottom = 12f;
            gameCard.Add(_difficultyHint);

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

        VisualElement ContinueCard(SaveFile saved)
        {
            var card = Ui.Row();
            card.style.backgroundColor = new Color(0.25f, 0.21f, 0.13f);
            Ui.Radius(card, 10f);
            Ui.Border(card, 2f, Theme.Gold);
            Ui.Padding(card, 14f, 20f);
            card.style.marginBottom = 14f;

            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            texts.Add(Ui.Text("Незаконченная партия", 18f, Theme.Gold, true));
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
            else _characterHint.text = "Персонаж: нажмите на кнопку персонажа — откроется карточка с героями, их способностями и статистикой. «Выбор из двух» — в начале партии игрок видит двух случайных персонажей и стартовые контракты и выбирает одного; «случайный» раздаёт сразу.";
            if (_mapButton != null)
            {
                var map = GameSetup.MapOf(_setup.mapResourcePath);
                _mapButton.text = map.Title;
                _mapHint.text = map.Hint;
                _modeButton.text = _setup.magnate ? "Магнат" : _setup.cooperative ? "Кооператив" : "Соревнование";
                var rules = new GameConfig();
                var magnateRules = new MagnateConfig();
                if (_setup.magnate)
                {
                    _mapButton.text = "Новый город";
                    _mapHint.text = "Город генерируется заново при каждом запуске: реки, станции, аэропорты, вокзалы и порты.";
                }
                _modeHint.text = _setup.magnate
                    ? $"«Логистический магнат»: компании возят грузы с хабов — скоропорт, тяжеловес, опасный. Раунд стройки и {magnateRules.Rounds} раундов перевозок, ходы планируются тайно, фуры едут разом; перегруженная дорога — пробка."
                    : _setup.cooperative
                    ? $"«Город против всех»: все — одна команда. Выполните {rules.CoopOrdersToWin(_setup.players.Count)} заказов города, пока кризис не дошёл до {rules.CoopCrisisMax}. Проезд между союзниками бесплатный."
                    : "Каждый сам за себя: больше всех очков — победа.";
            }
            if (_scenarioButton != null)
            {
                var scenario = (ScenarioKind)Mathf.Clamp(_setup.scenario, 0, Scenarios.All.Count - 1);
                _scenarioButton.text = Scenarios.Title(scenario);
                _scenarioHint.text = scenario == ScenarioKind.Standard
                    ? Scenarios.Summary(scenario)
                    : string.Join("\n", Scenarios.Changes(scenario).Select(c => "• " + c));
            }
            _trainsValue.text = _setup.roadPieces > 0 ? _setup.roadPieces.ToString() : "по правилам";
            _hideCardsButton.text = _setup.hideCardsBetweenTurns ? "Да" : "Нет";
            _timerButton.text = _setup.turnSeconds > 0 ? $"{_setup.turnSeconds / 60}:{_setup.turnSeconds % 60:00}" : "Без таймера";

            // В магнате нет сценариев, участков, карт на руке, персонажей и таймера хода — эти строки прячем.
            bool classic = !_setup.magnate;
            // У магната город генерируется заново — выбирать карту не нужно.
            Ui.Show(_mapRow, classic);
            Ui.Show(_mapHint, classic);
            Ui.Show(_scenarioRow, classic);
            Ui.Show(_scenarioHint, classic);
            Ui.Show(_piecesRow, classic);
            Ui.Show(_hideRow, classic);
            Ui.Show(_timerRow, classic);
            Ui.Show(_difficultyRow, !classic);
            Ui.Show(_difficultyHint, !classic);
            var difficulty = (MagnateDifficulty)Mathf.Clamp(_setup.magnateDifficulty, 0, 3);
            if (_difficultyButton != null) _difficultyButton.text = MagnateConfig.Title(difficulty);
            if (_difficultyHint != null) _difficultyHint.text = DifficultyHint(difficulty);
            Ui.Show(_characterHint, classic);

            int humans = _setup.players.Count(p => !p.isBot);
            _hint.text = _setup.magnate
                ? (humans > 1 ? "Игра за одним компьютером: перед каждым ходом заставка — соперники не видят ваши маршруты." : "Наведите на дорогу или станцию — подсказка покажет пропускную способность, владельца и грузы.")
                : humans == 0
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

            // Персонаж: кнопка открывает окно выбора с карточкой персонажа (MainMenuView.CharacterPicker).
            var character = Ui.Button(CharacterLabel(slot), () => OpenCharacterPicker(index), ButtonKind.Secondary, 14f);
            character.style.width = 190f;
            character.style.marginRight = 8f;
            // Значок рядом с кнопкой (у кнопки-текста свои дети накладывались бы на подпись).
            var icon = new CharacterIcon(30f, slot.character > 0 ? (CharacterKind)slot.character : CharacterKind.None,
                Palette.PlayerColor(index));
            icon.style.marginRight = 6f;
            icon.style.opacity = slot.character > 0 ? 1f : 0.35f;
            icon.pickingMode = PickingMode.Position;
            icon.RegisterCallback<ClickEvent>(_ => OpenCharacterPicker(index));
            // В магнате персонажей нет — кнопку не показываем.
            if (!_setup.magnate)
            {
                row.Add(icon);
                row.Add(character);
            }

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
            slot.character == PlayerSlot.DraftCharacter ? "Персонаж: выбор из двух"
            : slot.character == PlayerSlot.RandomCharacter ? "Персонаж: случайный"
            : Characters.Title((CharacterKind)slot.character);

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

        UnityEngine.UIElements.Button _scenarioButton;
        Label _scenarioHint;
        UnityEngine.UIElements.Button _mapButton, _modeButton;
        Label _mapHint, _modeHint;

        static Label Hint()
        {
            var hint = Ui.Text("", 12f, Theme.TextDim);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = -4f;
            hint.style.marginBottom = 12f;
            return hint;
        }

        void NextMap()
        {
            if (_setup.magnate) return; // в магнате город генерируется сам
            int index = System.Array.FindIndex(GameSetup.Maps, m => m.Path == GameSetup.MapOf(_setup.mapResourcePath).Path);
            _setup.mapResourcePath = GameSetup.Maps[(index + 1) % GameSetup.Maps.Length].Path;
            Refresh();
        }

        void NextScenario()
        {
            _setup.scenario = (Mathf.Clamp(_setup.scenario, 0, Scenarios.All.Count - 1) + 1) % Scenarios.All.Count;
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

        void NextDifficulty()
        {
            _setup.magnateDifficulty = (Mathf.Clamp(_setup.magnateDifficulty, 0, 3) + 1) % 4;
            _setup.Save();
            Refresh();
        }

        static string DifficultyHint(MagnateDifficulty difficulty)
        {
            switch (difficulty)
            {
                case MagnateDifficulty.Easy: return "Грузов больше, награды ×1,2, событий и краж меньше, на старте 8 очков стройки.";
                case MagnateDifficulty.Hard: return "Грузов меньше, награды ×0,9, событий больше, пробка стоит −2.";
                case MagnateDifficulty.Expert: return "Мало грузов, награды ×0,8, частые события и кражи, пробка −2, на старте 4 очка стройки и +3 за раунд.";
                default:
                    var normal = new MagnateConfig();
                    return $"Обычные правила: раунд стройки и {normal.Rounds} раундов перевозок, {normal.TrucksPerPlayer} фуры, " +
                           $"{normal.StartBuildPoints} очков стройки на старте и +{normal.BuildPointsPerRound} за раунд.";
            }
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

        void StartGame() => _app.StartGame(_setup);
    }
}
