using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(volume);

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
