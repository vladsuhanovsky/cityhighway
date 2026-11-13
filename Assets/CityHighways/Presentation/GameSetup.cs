using System;
using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    public enum BotLevel
    {
        /// <summary>Строит сеть под контракты и умеет пользоваться платным проездом.</summary>
        Smart,
        /// <summary>Случайные разрешённые ходы — для разминки.</summary>
        Random
    }

    /// <summary>Участник партии: имя, человек или бот, уровень бота.</summary>
    [Serializable]
    public class PlayerSlot
    {
        public string name = "Игрок";
        public bool isBot;
        public BotLevel botLevel = BotLevel.Smart;
        /// <summary>Персонаж: -1 — случайный, иначе CharacterKind как int (0 — без персонажа).</summary>
        public int character = RandomCharacter;

        public const int RandomCharacter = -1;

        public PlayerSlot() { }

        public PlayerSlot(string name, bool isBot, BotLevel botLevel = BotLevel.Smart)
        {
            this.name = name;
            this.isBot = isBot;
            this.botLevel = botLevel;
        }

        public IBot CreateBot(int seed) => botLevel == BotLevel.Random ? (IBot)new RandomBot(seed) : new SmartBot(seed);
    }

    /// <summary>Настройки партии из главного меню. Запоминаются между запусками (PlayerPrefs).</summary>
    [Serializable]
    public class GameSetup
    {
        const string PrefsKey = "CityHighways.Setup.v1";

        public List<PlayerSlot> players = new List<PlayerSlot>();
        /// <summary>0 — по числу игроков (как в правилах), иначе у всех ровно столько участков.</summary>
        public int roadPieces;
        public bool hideCardsBetweenTurns = true;
        /// <summary>Таймер хода человека в секундах; 0 — без таймера. Время вышло — ход доигрывает бот.</summary>
        public int turnSeconds;

        public static readonly int[] TurnTimerOptions = { 0, 60, 90, 120, 180 };
        public bool sound = true;
        public string mapResourcePath = "CityHighways/Maps/moscow";

        public static readonly string[] DefaultNames = { "Аня", "Борис", "Вера", "Глеб", "Даша" };

        public static GameSetup CreateDefault()
        {
            var setup = new GameSetup();
            setup.players.Add(new PlayerSlot("Аня", false));
            setup.players.Add(new PlayerSlot("Борис", true));
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

        /// <summary>
        /// Персонажи для партии: выбранные остаются, «случайным» раздаются разные персонажи
        /// из тех, что никто не выбрал (если таких не хватает — любые).
        /// </summary>
        public List<CharacterKind> ResolveCharacters(System.Random rng)
        {
            var chosen = new HashSet<CharacterKind>();
            foreach (var slot in players)
                if (slot.character > 0) chosen.Add((CharacterKind)slot.character);
            var free = new List<CharacterKind>();
            foreach (var kind in Characters.All)
                if (!chosen.Contains(kind)) free.Add(kind);

            var result = new List<CharacterKind>();
            foreach (var slot in players)
            {
                if (slot.character != PlayerSlot.RandomCharacter)
                {
                    result.Add((CharacterKind)Mathf.Clamp(slot.character, 0, (int)CharacterKind.Bridger));
                    continue;
                }
                var pool = free.Count > 0 ? free : new List<CharacterKind>(Characters.All);
                var pick = pool[rng.Next(pool.Count)];
                free.Remove(pick);
                result.Add(pick);
            }
            return result;
        }

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
