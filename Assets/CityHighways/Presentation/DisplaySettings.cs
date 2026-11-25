using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Настройки экрана и громкости. Хранятся в PlayerPrefs, применяются при запуске.</summary>
    [Serializable]
    public class DisplaySettings
    {
        const string PrefsKey = "CityHighways.Display.v1";

        public bool fullscreen = true;
        public int width;
        public int height;
        public float volume = 0.8f;
        /// <summary>Original procedural background music, opt-in for existing players.</summary>
        public bool musicEnabled;
        /// <summary>Music level independent of SFX; master volume still applies.</summary>
        public float musicVolume = MusicSoundscape.DefaultVolume;
        /// <summary>Игровое поле с лёгким наклоном (2.5D); выключено — плоская карта сверху.</summary>
        public bool tiltedBoard = true;
        /// <summary>Мини-карта в углу игрового поля.</summary>
        public bool showMinimap = true;
        /// <summary>3D-город под картой: здания, деревья, река с набережными, мосты, свет и тени.</summary>
        public bool city3D = true;
        /// <summary>Качество 3D-города: 0 — низкое (меньше зданий, без теней), 1 — среднее, 2 — высокое.</summary>
        public int city3DQuality = 2;
        /// <summary>Наклон камеры 3D-города от вертикали, градусы (TiltOptions).</summary>
        public float tiltDegrees = 38f;

        public static readonly float[] TiltOptions = { 30f, 38f, 45f };
        public static string[] City3DQualityNames => LocalizationService.Language == "en" ? new[] { "Low", "Medium", "High" } : new[] { "Низкое", "Среднее", "Высокое" };
        /// <summary>Свет фона главного меню: 0 — по часам компьютера, 1 — день, 2 — ночь.</summary>
        public int menuLight;
        /// <summary>Палитра фона главного меню (MenuBackground.PaletteNames).</summary>
        public int menuPalette;
        /// <summary>Картина города (Resources/CityHighways/UI/key_art) вместо живого фона, если картинка есть.</summary>
        public bool menuKeyArt = true;
        /// <summary>Звуки интерфейса: щелчки кнопок и тихие звуки наведения.</summary>
        public bool uiSounds = true;
        /// <summary>Доступность: меньше анимаций (переходы почти мгновенные).</summary>
        public bool reducedMotion;
        /// <summary>Optional alternative company colors; default palette stays unchanged.</summary>
        public bool accessiblePlayerColors;
        /// <summary>Stable seat numbers in HUD and matching road ownership pips.</summary>
        public bool showOwnerMarkers;
        /// <summary>Доступность: масштаб интерфейса, 0,9–1,3.</summary>
        public float uiScale = 1f;
        /// <summary>Уровень качества Unity (Project Settings → Quality); −1 — не трогать.</summary>
        public int quality = -1;

        public static readonly float[] UiScales = { 0.9f, 1f, 1.1f, 1.2f, 1.3f };

        public static int UiScaleIndex(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return 1;
            if (value <= UiScales[0]) return 0;
            if (value >= UiScales[UiScales.Length - 1]) return UiScales.Length - 1;
            int best = 0;
            for (int i = 1; i < UiScales.Length; i++)
                if (Mathf.Abs(value - UiScales[i]) < Mathf.Abs(value - UiScales[best])) best = i;
            return best;
        }

        public static float NormalizeUiScale(float value) => UiScales[UiScaleIndex(value)];

        // ---- Управление камерой (Настройки → Управление)
        /// <summary>Скорость поворота и наклона камеры (мышь и клавиши Q/E, R/F): 0 — медленно, 1 — обычно, 2 — быстро.</summary>
        public int cameraSpeed = 1;
        /// <summary>Скорость приближения колесом: 0 — плавно, 1 — обычно, 2 — быстро.</summary>
        public int zoomSpeed = 1;
        /// <summary>Инвертировать наклон: мышь вверх — камера ближе к горизонту.</summary>
        public bool invertTilt;
        /// <summary>Плавное вращение: камера догоняет мышь за несколько кадров; выключено — поворот сразу за курсором.</summary>
        public bool smoothCamera = true;
        /// <summary>
        /// Левая кнопка мыши крутит и наклоняет 3D-поле (тянуть), а клик без перетаскивания выбирает дорогу или станцию;
        /// сдвиг приближенной карты — правой или средней кнопкой. Выключено — как раньше: левая тянет карту, правая крутит.
        /// </summary>
        public bool leftDragRotates = true;

        /// <summary>Чем крутится поле — для подсказок.</summary>
        public static string RotateButtonText => Active == null || Active.leftDragRotates ? "левой кнопкой мыши" : "правой кнопкой мыши";

        public static string[] SpeedNames => LocalizationService.Language == "en" ? new[] { "Slow", "Normal", "Fast" } : new[] { "Медленно", "Обычно", "Быстро" };
        static readonly float[] SpeedFactors = { 0.6f, 1f, 1.6f };
        public float CameraSpeedFactor => SpeedFactors[Mathf.Clamp(cameraSpeed, 0, 2)];
        public float ZoomStep => new[] { 1.1f, 1.2f, 1.35f }[Mathf.Clamp(zoomSpeed, 0, 2)];

        /// <summary>Настройки, с которыми работает игра (общий объект приложения); задаётся при запуске.</summary>
        public static DisplaySettings Active;

        /// <summary>Панель интерфейса — ей меняем масштаб; задаёт приложение при запуске.</summary>
        public static PanelSettings Panel;

        public enum Preset { LowEnd, Cinematic, Esports }

        public static string PresetTitle(Preset preset) =>
            LocalizationService.Language == "en"
                ? (preset == Preset.LowEnd ? "Low-end PC" : preset == Preset.Cinematic ? "Cinematic" : "Esports")
                : (preset == Preset.LowEnd ? "Для слабого ПК" : preset == Preset.Cinematic ? "Кинематографичный" : "Киберспорт");

        public static string PresetHint(Preset preset) =>
            LocalizationService.Language == "en"
                ? (preset == Preset.LowEnd ? "Lower quality, flat map without a 3D city, fewer animations"
                    : preset == Preset.Cinematic ? "Maximum quality, 3D city at 45°, all animations, time-based lighting"
                    : "Top-down 3D city for clarity, minimap, fewer animations")
                : (preset == Preset.LowEnd ? "Низкое качество, плоская карта без 3D-города, меньше анимаций"
                    : preset == Preset.Cinematic ? "Максимум качества, 3D-город под углом 45°, все анимации, свет по часам"
                    : "3D-город сверху для чёткости, мини-карта, без лишних анимаций");

        /// <summary>Пресет меняет графику и анимации; громкость, экран и разрешение не трогает.</summary>
        public void ApplyPreset(Preset preset)
        {
            int top = Mathf.Max(0, QualitySettings.names.Length - 1);
            switch (preset)
            {
                case Preset.LowEnd:
                    quality = 0;
                    tiltedBoard = false;
                    reducedMotion = true;
                    showMinimap = true;
                    city3D = false;
                    city3DQuality = 0;
                    break;
                case Preset.Cinematic:
                    quality = top;
                    tiltedBoard = true;
                    reducedMotion = false;
                    showMinimap = false;
                    menuLight = 0;
                    city3D = true;
                    city3DQuality = 2;
                    tiltDegrees = 45f;
                    break;
                default:
                    quality = top;
                    tiltedBoard = false;
                    reducedMotion = true;
                    showMinimap = true;
                    city3D = true;
                    city3DQuality = 1;
                    break;
            }
        }

        /// <summary>Заводские настройки: всё, кроме режима экрана и разрешения (их берём у монитора).</summary>
        public static DisplaySettings Defaults() => new DisplaySettings
        {
            fullscreen = true,
            width = Screen.currentResolution.width,
            height = Screen.currentResolution.height
        };

        public static DisplaySettings Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var settings = FromJson(json);
                    if (settings != null) return settings;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось прочитать настройки экрана: " + e.Message);
            }

            return new DisplaySettings
            {
                fullscreen = Screen.fullScreen,
                width = Screen.currentResolution.width,
                height = Screen.currentResolution.height
            };
        }

        /// <summary>
        /// Backwards-compatible audio migration for Display.v1 saves:
        /// absent music fields must not turn background music on, and
        /// absent volume must not accidentally initialize at zero.
        /// An explicitly saved zero remains a valid mute setting.
        /// </summary>
        public static DisplaySettings FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var settings = JsonUtility.FromJson<DisplaySettings>(json);
            if (settings == null) return null;
            if (!json.Contains("\"musicVolume\""))
                settings.musicVolume = MusicSoundscape.DefaultVolume;
            settings.musicVolume = MusicSoundscape.SafeVolume(settings.musicVolume);
            settings.uiScale = NormalizeUiScale(settings.uiScale);
            return settings;
        }

        /// <summary>Переписать все поля из other (объект настроек общий у приложения, заменять его нельзя).</summary>
        public void CopyFrom(DisplaySettings other)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(other), this);
            uiScale = NormalizeUiScale(uiScale);
        }

        public void Save()
        {
            uiScale = NormalizeUiScale(uiScale);
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(volume);
            Ui.ReducedMotion = reducedMotion;
            uiScale = NormalizeUiScale(uiScale);
            if (Panel != null) Panel.scale = uiScale;
            if (quality >= 0 && quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != quality)
                QualitySettings.SetQualityLevel(quality, true);

            // В редакторе размером окна управляет Game View — меняем только в собранной игре.
            if (Application.isEditor) return;
            int w = width > 0 ? width : Screen.currentResolution.width;
            int h = height > 0 ? height : Screen.currentResolution.height;
            Screen.SetResolution(w, h, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        /// <summary>Разрешения монитора (без повторов из-за разной частоты), от большего к меньшему.</summary>
        public static List<Vector2Int> AvailableResolutions()
        {
            var list = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Where(r => r.y >= 720)
                .Distinct()
                .OrderByDescending(r => r.x * r.y)
                .ToList();
            if (list.Count == 0) list.Add(new Vector2Int(1920, 1080));
            return list;
        }
    }
}
