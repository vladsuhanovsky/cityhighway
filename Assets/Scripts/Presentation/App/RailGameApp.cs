using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace RailGame.Presentation.App
{
    /// <summary>
    /// Точка входа игры на UI Toolkit: главное меню → партия → итоги.
    /// Весь интерфейс собирается кодом, поэтому достаточно повесить компонент
    /// на пустой объект в сцене и нажать Play.
    /// </summary>
    public sealed class RailGameApp : MonoBehaviour
    {
        [Tooltip("Необязательно. Panel Settings с темой (Assets → Create → UI Toolkit → Panel Settings Asset). " +
                 "Если пусто, настройки создаются кодом.")]
        [SerializeField] PanelSettings panelSettings;

        [Tooltip("Необязательно. Шрифт с кириллицей; если пусто, берётся встроенный.")]
        [SerializeField] Font font;

        public Tweener Tweener { get; } = new Tweener();
        public SoundBank Sound { get; private set; }

        VisualElement _root;
        GameView _game;

        IEnumerator Start()
        {
            var settings = panelSettings != null ? panelSettings : CreatePanelSettings();

            // UIDocument создаём на выключенном объекте: при включении он сразу построит корень с нашими настройками.
            var uiObject = new GameObject("RailGame UI");
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
                Debug.LogError("UIDocument не создал интерфейс. Назначьте Panel Settings в инспекторе RailGameApp.");
                yield break;
            }
            _root.style.flexGrow = 1f;
            _root.style.backgroundColor = Theme.Background;
            var usedFont = font != null ? font : LoadBuiltinFont();
            if (usedFont != null) _root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(usedFont));

            var audio = gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            Sound = new SoundBank(audio);
            Ui.ButtonClicked = () => Sound.Play(Sfx.Click, 0.6f);

            ShowMenu();
        }

        void Update()
        {
            Tweener.Tick(Time.deltaTime);
            _game?.Tick(Time.deltaTime);
        }

        public void ShowMenu()
        {
            if (_root == null) return;
            Tweener.Clear();
            _game = null;
            _root.Clear();
            var setup = GameSetup.Load();
            Sound.Enabled = setup.sound;
            _root.Add(new MainMenuView(this, setup).Root);
        }

        public void StartGame(GameSetup setup)
        {
            setup.Save();
            Sound.Enabled = setup.sound;
            Tweener.Clear();
            _root.Clear();
            _game = new GameView(this, setup);
            _root.Add(_game.Root);
            _game.StartNewGame();
        }

        static PanelSettings CreatePanelSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "RailGame Panel Settings";
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
