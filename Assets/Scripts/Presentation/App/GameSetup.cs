using System;
using System.Collections.Generic;
using RailGame.Core;
using UnityEngine;

namespace RailGame.Presentation.App
{
    /// <summary>Настройки партии из главного меню. Запоминаются между запусками (PlayerPrefs).</summary>
    [Serializable]
    public class GameSetup
    {
        const string PrefsKey = "RailGame.Setup.v1";

        public List<PlayerSlot> players = new List<PlayerSlot>();
        public int trainsPerPlayer = 30;
        public bool hideCardsBetweenTurns = true;
        public bool sound = true;
        public string mapResourcePath = "Maps/demo_map";

        public static readonly string[] DefaultNames = { "Аня", "Борис", "Вера", "Глеб", "Даша" };

        public static GameSetup CreateDefault()
        {
            var setup = new GameSetup();
            setup.players.Add(new PlayerSlot("Аня", false));
            setup.players.Add(new PlayerSlot("Борис", true, BotLevel.Smart));
            return setup;
        }

        public static GameSetup Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(PrefsKey, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var setup = JsonUtility.FromJson<GameSetup>(json);
                    if (setup != null && setup.players != null && setup.players.Count >= 2) return setup;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Не удалось прочитать настройки, беру стандартные: " + e.Message);
            }
            return CreateDefault();
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        /// <summary>Имена без пустых и с ограничением длины — то, что пойдёт в игру.</summary>
        public List<string> PlayerNames()
        {
            var names = new List<string>();
            for (int i = 0; i < players.Count; i++)
            {
                var name = (players[i].name ?? "").Trim();
                if (name.Length == 0) name = "Игрок " + (i + 1);
                if (name.Length > 16) name = name.Substring(0, 16);
                names.Add(name);
            }
            return names;
        }
    }
}
