using System;
using System.IO;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    [Serializable]
    public class SaveFile
    {
        public GameSetup setup;
        public GameSaveData game;
        public string savedAt;
        public float playSeconds;
    }

    /// <summary>
    /// Автосохранение незаконченной партии в файл cityhighways_save.json
    /// (Windows: %USERPROFILE%\AppData\LocalLow\&lt;компания&gt;\&lt;игра&gt;).
    /// </summary>
    public static class SaveSystem
    {
        static string SavePath => Path.Combine(Application.persistentDataPath, "cityhighways_save.json");

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(GameSetup setup, Game game, float playSeconds)
        {
            if (game == null || game.Phase == GamePhase.GameOver) return;
            try
            {
                var file = new SaveFile
                {
                    setup = setup,
                    game = game.CreateSave(),
                    savedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                    playSeconds = playSeconds
                };

                // Пишем во временный файл и подменяем: если игру закроют посреди записи, старое сохранение уцелеет.
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(file));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(temp, SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось сохранить партию: " + e.Message);
            }
        }

        public static SaveFile Load()
        {
            try
            {
                if (!HasSave) return null;
                var file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(SavePath));
                if (file == null || file.game == null || file.setup == null || file.setup.players == null) return null;
                return file;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось прочитать сохранение: " + e.Message);
                return null;
            }
        }

        public static void Delete()
        {
            try
            {
                if (HasSave) File.Delete(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось удалить сохранение: " + e.Message);
            }
        }
    }
}
