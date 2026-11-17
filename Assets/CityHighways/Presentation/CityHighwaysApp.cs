using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.IO;
using CityHighways.Core;
using CityHighways.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Точка входа City Highways на UI Toolkit: главное меню → партия → итоги.
    /// Весь интерфейс собирается кодом, поэтому достаточно повесить компонент
    /// на пустой объект в сцене и нажать Play.
    /// </summary>
    public sealed class CityHighwaysApp : MonoBehaviour
    {
        [Tooltip("Необязательно. Panel Settings с темой (Assets → Create → UI Toolkit → Panel Settings Asset). " +
                 "Если пусто, настройки создаются кодом.")]
        [SerializeField] PanelSettings panelSettings;

        [Tooltip("Необязательно. Шрифт с кириллицей; если пусто, берётся встроенный.")]
        [SerializeField] Font font;

        public Tweener Tweener { get; } = new Tweener();
        public SoundBank Sound { get; private set; }
        public DisplaySettings Display { get; private set; }

        VisualElement _root;
        GameView _game;
        /// <summary>Партия «Логистического магната» (вместо _game).</summary>
        MagnateView _magnate;
        MainMenuView _menu;

        /// <summary>Текущая сетевая сессия (лобби или партия); null — сети нет.</summary>
        public NetSession Net { get; private set; }

        IEnumerator Start()
        {
            // Настройки из инспектора копируем, чтобы не менять ассет в редакторе (тема ниже — объект времени выполнения).
            var settings = panelSettings != null ? Instantiate(panelSettings) : CreatePanelSettings();
            if (settings.themeStyleSheet == null) settings.themeStyleSheet = LoadTheme();

            // UIDocument создаём на выключенном объекте: при включении он сразу построит корень с нашими настройками.
            var uiObject = new GameObject("City Highways UI");
            uiObject.SetActive(false);
            uiObject.transform.SetParent(transform, false);
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            uiObject.SetActive(true);

            // Обычно корень готов сразу; на всякий случай ждём кадр-другой.
            for (int frame = 0; frame < 3 && document.rootVisualElement == null; frame++) yield return null;
            _root = document.rootVisualElement;
            if (_root == null)
            {
                Debug.LogError("UIDocument не создал интерфейс. Назначьте Panel Settings в инспекторе CityHighwaysApp.");
                yield break;
            }
            _root.style.flexGrow = 1f;
            _root.style.backgroundColor = Theme.Background;
            // Свой шрифт из инспектора, иначе Golos Text из Resources, иначе встроенный.
            var usedFont = font != null ? font : Fonts.Regular != null ? Fonts.Regular : LoadBuiltinFont();
            if (usedFont != null) _root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(usedFont));

            Display = DisplaySettings.Load();
            DisplaySettings.Panel = settings;
            Display.Apply();

            var audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            Sound = new SoundBank(audio);
            Ui.ButtonClicked = () => { if (Display.uiSounds) Sound.Play(Sfx.Click, 0.6f); };
            Ui.Hovered = () => { if (Display.uiSounds) Sound.Play(Sfx.Tick, 0.3f); };
            Ui.Changed = () => { if (Display.uiSounds) Sound.Play(Sfx.Tick, 0.55f); };

            ShowMenu();

            // Steam: приглашение друга (оверлей) или запуск игры с +connect_lobby.
            var steam = NetPlatforms.Steam;
            if (steam != null && steam.Available)
            {
                steam.JoinRequested += OnSteamJoinRequested;
                ulong pending = steam.TakePendingJoin();
                if (pending != 0) OnSteamJoinRequested(pending);
            }
        }

        void OnSteamJoinRequested(ulong lobby)
        {
            if (_game != null || _magnate != null) return; // посреди партии приглашения не принимаем
            if (_menu == null) ShowMenu();
            _menu?.JoinSteamLobby(lobby);
        }

        void Update()
        {
            NetPlatforms.Steam?.Poll();
            Net?.Poll();
            Tweener.Tick(Time.deltaTime);
            _game?.Tick(Time.deltaTime);
            _magnate?.Tick(Time.deltaTime);
            _menu?.Tick(Time.deltaTime);
        }

        void OnDestroy()
        {
            _game?.Release3D();
            _magnate?.Release3D();
            _magnate = null;
            CloseNet();
            NetPlatforms.Steam?.Shutdown();
        }

        /// <summary>Под интерфейсом рисуется 3D-сцена: корень прозрачный, иначе — сплошной фон.</summary>
        public void SetSceneBehind(bool on)
        {
            if (_root != null) _root.style.backgroundColor = on ? Color.clear : Theme.Background;
        }

        public void ShowMenu()
        {
            if (_root == null) return;
            CloseNet();
            Tweener.Clear();
            _game?.Release3D();
            _magnate?.Release3D();
            _magnate = null;
            _game = null;
            _root.Clear();
            var setup = GameSetup.Load();
            Sound.Enabled = setup.sound;
            _menu = new MainMenuView(this, setup);
            _root.Add(_menu.Root);
        }

        /// <summary>Начать сетевую сессию (лобби); прежняя закрывается.</summary>
        public void OpenNet(NetSession session)
        {
            CloseNet();
            Net = session;
        }

        public void CloseNet()
        {
            if (Net == null) return;
            var session = Net;
            Net = null;
            try
            {
                session.Dispose();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Сеть закрылась с ошибкой: " + e.Message);
            }
        }

        /// <summary>Сетевая партия началась: настройки берутся из мест лобби, сессия остаётся открытой.</summary>
        public void StartOnlineGame()
        {
            if (Net == null || Net.Game == null) return;
            var local = GameSetup.Load();
            var setup = new GameSetup
            {
                sound = local.sound,
                hideCardsBetweenTurns = false,
                turnSeconds = Net.TurnSeconds,
                mapResourcePath = Net.MapPath
            };
            foreach (var seat in Net.Seats)
                setup.players.Add(new PlayerSlot(seat.name, seat.isBot, seat.botLevel == 1 ? BotLevel.Random : BotLevel.Smart));
            Sound.Enabled = setup.sound;
            Tweener.Clear();
            _root.Clear();
            _menu = null;
            _game?.Release3D();
            _magnate?.Release3D();
            _magnate = null;
            _game = new GameView(this, setup, Net);
            _root.Add(_game.Root);
            _game.StartOnline();
        }

        public void StartGame(GameSetup setup)
        {
            if (setup.magnate)
            {
                StartMagnate(setup);
                return;
            }
            setup.Save();
            Sound.Enabled = setup.sound;
            CloseNet();
            Tweener.Clear();
            _root.Clear();
            _menu = null;
            _game?.Release3D();
            _magnate?.Release3D();
            _magnate = null;
            _game = new GameView(this, setup);
            _root.Add(_game.Root);
            _game.StartNewGame();
        }

        /// <summary>
        /// «Логистический магнат»: экран загрузки, город генерируется заново (каждый раз новое зерно),
        /// создаётся партия и экран, строится 3D-город — по шагу за кадр, чтобы полоса загрузки двигалась.
        /// </summary>
        /// <summary>
        /// Город для магната с проверкой при запуске: места не в реке, дороги без моста не идут по воде, мосты
        /// пересекают реку (CityGenerator.CheckWater — по тем же сглаженным руслам, что рисует 3D). Если что-то не так —
        /// берём другой город, о каждом отбракованном пишем в консоль.
        /// </summary>
        static GeneratedCity GenerateCheckedCity(int seed)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var city = CityGenerator.Generate(seed + attempt * 104729);
                var issues = CityGenerator.CheckWater(city.Map);
                if (issues.Count == 0) return city;
                Debug.LogWarning($"Город {city.Map.Name} (seed {city.Seed}) отбракован проверкой воды:\n" + string.Join("\n", issues));
            }
            throw new InvalidOperationException("Не удалось сгенерировать город без ошибок на воде.");
        }

        public void StartMagnate(GameSetup setup)
        {
            setup.Save();
            Sound.Enabled = setup.sound;
            CloseNet();
            Tweener.Clear();
            _root.Clear();
            _menu = null;
            _game?.Release3D();
            _game = null;
            _magnate?.Release3D();
            _magnate = null;

            var loading = new MagnateLoading("Логистический магнат");
            _root.Add(loading.Root);
            int seed = Environment.TickCount & 0x7FFFFFFF;
            var names = setup.players.Select(p => p.name).ToList();
            var bots = setup.players.Select(p => p.isBot).ToList();
            GeneratedCity city = null;
            LogisticsGame game = null;
            MagnateView view = null;
            var steps = new List<(string, Action)>
            {
                ("Прокладываем реки и намечаем кварталы…", () => city = GenerateCheckedCity(seed)),
                ("Расставляем аэропорты, вокзалы и порты…", () => game = new LogisticsGame(city, names, bots, seed,
                    MagnateConfig.For((MagnateDifficulty)Mathf.Clamp(setup.magnateDifficulty, 0, 3)))),
                ("Рисуем карту и дороги…", () =>
                {
                    view = new MagnateView(this, setup, game);
                    _root.Insert(0, view.Root);
                    view.Begin();
                }),
                ("Поднимаем дома, сажаем деревья, наполняем реки…", () => view.Setup3D()),
                ("Грузим первые фуры…", () => { }),
            };
            loading.Run(this, steps, () =>
            {
                _magnate = view;
                loading.Root.RemoveFromHierarchy();
            }, () =>
            {
                view?.Release3D();
                ShowMenu();
            });
        }

        /// <summary>Обучающая партия: настройки и сохранение игрока не трогает.</summary>
        public void StartTutorial()
        {
            var local = GameSetup.Load();
            var setup = GameSetup.CreateTutorial(Profiles.Active?.name, local.sound);
            Sound.Enabled = setup.sound;
            CloseNet();
            Tweener.Clear();
            _root.Clear();
            _menu = null;
            _game?.Release3D();
            _magnate?.Release3D();
            _magnate = null;
            _game = new GameView(this, setup);
            _root.Add(_game.Root);
            _game.StartNewGame();
        }

        public void ContinueGame(SaveFile file)
        {
            if (file == null) return;
            Sound.Enabled = file.setup.sound;
            CloseNet();
            Tweener.Clear();
            _root.Clear();
            _menu = null;
            _game?.Release3D();
            _magnate?.Release3D();
            _magnate = null;
            _game = new GameView(this, file.setup);
            _root.Add(_game.Root);
            _game.ContinueGame(file);
        }

        /// <summary>Загружает карту города из Resources (путь без расширения, например "CityHighways/Maps/moscow").</summary>
        public static CityMap LoadMap(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null) throw new FileNotFoundException($"Не найден файл карты Resources/{resourcePath}.json");
            return JsonUtility.FromJson<CityMapDto>(asset.text).ToMap();
        }

        static PanelSettings CreatePanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "City Highways Panel Settings";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f; // подстраиваемся по высоте: интерфейс одинаков на 16:9 и 16:10
            settings.themeStyleSheet = LoadTheme();
            return settings;
        }

        /// <summary>
        /// Тема панели. Весь интерфейс оформлен прямо в коде и рассчитан на «чистую» тему без стандартных стилей
        /// Unity, поэтому по умолчанию берётся пустая тема: так пропадает предупреждение «No Theme Style Sheet set
        /// to PanelSettings», а вид остаётся прежним. Если положить свою тему в
        /// Resources/CityHighways/UI/CityHighwaysTheme.tss, возьмётся она.
        /// </summary>
        static ThemeStyleSheet LoadTheme()
        {
            var theme = Resources.Load<ThemeStyleSheet>("CityHighways/UI/CityHighwaysTheme");
            if (theme != null) return theme;
            theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            theme.name = "City Highways Theme";
            theme.hideFlags = HideFlags.DontSave;
            return theme;
        }

        static Font LoadBuiltinFont()
        {
            // В Unity 2022.2+ встроенный шрифт называется LegacyRuntime.ttf, раньше — Arial.ttf.
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    var builtin = Resources.GetBuiltinResource<Font>(name);
                    if (builtin != null) return builtin;
                }
                catch
                {
                    // пробуем следующее имя
                }
            }
            return null;
        }
    }
}
