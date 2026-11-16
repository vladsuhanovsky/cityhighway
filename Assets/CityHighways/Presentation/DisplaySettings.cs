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
        /// <summary>Игровое поле с лёгким наклоном (2.5D); выключено — плоская карта сверху.</summary>
        public bool tiltedBoard = true;
        /// <summary>Мини-карта в углу игрового поля.</summary>
        public bool showMinimap = true;
        /// <summary>Свет фона главного меню: 0 — по часам компьютера, 1 — день, 2 — ночь.</summary>
        public int menuLight;
        /// <summary>Палитра фона главного меню (MenuBackground.PaletteNames).</summary>
        public int menuPalette;
        /// <summary>Звуки интерфейса: щелчки кнопок и тихие звуки наведения.</summary>
        public bool uiSounds = true;
        /// <summary>Доступность: меньше анимаций (переходы почти мгновенные).</summary>
        public bool reducedMotion;
        /// <summary>Доступность: масштаб интерфейса, 0,9–1,3.</summary>
        public float uiScale = 1f;
        /// <summary>Уровень качества Unity (Project Settings → Quality); −1 — не трогать.</summary>
        public int quality = -1;

        public static readonly float[] UiScales = { 0.9f, 1f, 1.1f, 1.2f, 1.3f };

        /// <summary>Панель интерфейса — ей меняем масштаб; задаёт приложение при запуске.</summary>
        public static PanelSettings Panel;

        public enum Preset { LowEnd, Cinematic, Esports }

        public static string PresetTitle(Preset preset) =>
            preset == Preset.LowEnd ? "Для слабого ПК" : preset == Preset.Cinematic ? "Кинематографичный" : "Киберспорт";

        public static string PresetHint(Preset preset) =>
            preset == Preset.LowEnd ? "Низкое качество, плоское поле, меньше анимаций"
            : preset == Preset.Cinematic ? "Максимум качества, наклон 2.5D, все анимации, свет по часам"
            : "Плоское поле для чёткости, мини-карта, без лишних анимаций";

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
                    break;
                case Preset.Cinematic:
                    quality = top;
                    tiltedBoard = true;
                    reducedMotion = false;
                    showMinimap = false;
                    menuLight = 0;
                    break;
                default:
                    quality = top;
                    tiltedBoard = false;
                    reducedMotion = true;
                    showMinimap = true;
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
                    var settings = JsonUtility.FromJson<DisplaySettings>(json);
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

        /// <summary>Переписать все поля из other (объект настроек общий у приложения, заменять его нельзя).</summary>
        public void CopyFrom(DisplaySettings other) => JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(other), this);

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(volume);
            Ui.ReducedMotion = reducedMotion;
            if (Panel != null) Panel.scale = Mathf.Clamp(uiScale <= 0f ? 1f : uiScale, 0.8f, 1.4f);
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
