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

        static string PathFor(GameSetup setup) => setup != null && setup.IsCampaign
            ? CampaignStorage.CheckpointPath(setup.campaignOwner) : SavePath;

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
                SaveFileStorage.Write(PathFor(setup), file);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось сохранить партию: " + e.Message);
            }
        }

        public static SaveFile Load() => Read(SavePath, null);

        public static SaveFile LoadCampaign(string owner)
        {
            if (!CampaignStorage.ValidOwner(owner)) return null;
            return Read(CampaignStorage.CheckpointPath(owner), owner);
        }

        static SaveFile Read(string path, string owner)
        {
            try
            {
                return SaveFileStorage.Read(path, owner);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось прочитать сохранение: " + e.Message);
                return null;
            }
        }

        public static void Delete(GameSetup setup = null)
        {
            try
            {
                string path = PathFor(setup);
                SaveFileStorage.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось удалить сохранение: " + e.Message);
            }
        }
    }
}
