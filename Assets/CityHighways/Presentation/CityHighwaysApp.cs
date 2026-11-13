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
        MainMenuView _menu;

        /// <summary>Текущая сетевая сессия (лобби или партия); null — сети нет.</summary>
        public NetSession Net { get; private set; }

        IEnumerator Start()
        {
            var settings = panelSettings != null ? panelSettings : CreatePanelSettings();

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
            var usedFont = font != null ? font : LoadBuiltinFont();
            if (usedFont != null) _root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(usedFont));

            Display = DisplaySettings.Load();
            Display.Apply();

            var audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            Sound = new SoundBank(audio);
            Ui.ButtonClicked = () => Sound.Play(Sfx.Click, 0.6f);

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
            if (_game != null) return; // посреди партии приглашения не принимаем
            if (_menu == null) ShowMenu();
            _menu?.JoinSteamLobby(lobby);
        }

        void Update()
        {
            NetPlatforms.Steam?.Poll();
            Net?.Poll();
            Tweener.Tick(Time.deltaTime);
            _game?.Tick(Time.deltaTime);
        }

        void OnDestroy()
        {
            CloseNet();
            NetPlatforms.Steam?.Shutdown();
        }

        public void ShowMenu()
        {
            if (_root == null) return;
            CloseNet();
            Tweener.Clear();
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
            _game = new GameView(this, setup, Net);
            _root.Add(_game.Root);
            _game.StartOnline();
        }

        public void StartGame(GameSetup setup)
        {
            setup.Save();
            Sound.Enabled = setup.sound;
            CloseNet();
            Tweener.Clear();
            _root.Clear();
            _menu = null;
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
            return settings;
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
