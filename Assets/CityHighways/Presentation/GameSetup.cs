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
        /// <summary>Персонаж: -2 — выбор из двух в начале партии, -1 — случайный, иначе CharacterKind как int (0 — без персонажа).</summary>
        public int character = DraftCharacter;

        public const int RandomCharacter = -1;
        public const int DraftCharacter = -2;

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
        /// <summary>Сценарий партии (ScenarioKind как int).</summary>
        public int scenario;
        /// <summary>Кооператив «Город против всех»: все компании — одна команда против города.</summary>
        public bool cooperative;
        /// <summary>«Логистический магнат» — основной режим: город генерируется заново, компании возят грузы.</summary>
        public bool magnate = true;
        /// <summary>Сложность магната (MagnateDifficulty как int): 0 лёгкая, 1 обычная, 2 сложная, 3 эксперт.</summary>
        public int magnateDifficulty = 1;
        /// <summary>Обучение магната пройдено или пропущено — больше не показывать само.</summary>
        public bool magnateTutorialDone;
        /// <summary>Слои карты магната (битовая маска MagnateView.Layer): названия станций, грузы, фуры, свободные дороги, фокус.</summary>
        public int magnateLayers = 109;

        /// <summary>Тренировочная партия из раздела «Обучение»: её настройки не сохраняются поверх настроек игрока.</summary>
        [NonSerialized] public bool practice;
        /// <summary>Показать обучение магната в этой партии, даже если оно уже пройдено.</summary>
        [NonSerialized] public bool forceMagnateTutorial;
        /// <summary>Обучение магната — на маленькой учебной карте (CityGenerator.GenerateTutorial), партия короче.</summary>
        [NonSerialized] public bool magnateTutorialMap;

        /// <summary>Карта для меню: путь в Resources, название и одна строка описания.</summary>
        public sealed class MapChoice
        {
            public string Path;
            public string Title;
            public string Hint;
        }

        public static readonly MapChoice[] Maps =
        {
            new MapChoice { Path = "CityHighways/Maps/moscow", Title = "Москва",
                Hint = "Большой город: 66 мест, Москва-река, МКАД и три района на окраинах." },
            new MapChoice { Path = "CityHighways/Maps/amsterdam", Title = "Амстердам",
                Hint = "Компактный город каналов: залив Эй отрезает Норд, Амстел подходит к центру. Кольцо A10." },
            new MapChoice { Path = "CityHighways/Maps/chongqing", Title = "Чунцин",
                Hint = "Город мостов и гор: центр зажат между Янцзы и Цзялинцзян, к любому берегу — по мосту." },
        };

        public static MapChoice MapOf(string path) =>
            Array.Find(Maps, m => string.Equals(m.Path, path, StringComparison.OrdinalIgnoreCase)) ?? Maps[0];
        /// <summary>Обучающая партия: не сохраняется (ни настройки, ни партия) и не пишется в статистику.</summary>
        [NonSerialized] public bool tutorial;

        public const string TutorialMap = "CityHighways/Maps/tutorial";
        /// <summary>Участков у каждого в обучении: карта маленькая, партия — минут на десять.</summary>
        public const int TutorialPieces = 14;

        /// <summary>Настройки обучающей партии: вы против умного бота-наставника на карте центра.</summary>
        public static GameSetup CreateTutorial(string playerName, bool sound)
        {
            var setup = new GameSetup
            {
                tutorial = true,
                sound = sound,
                hideCardsBetweenTurns = false,
                turnSeconds = 0,
                roadPieces = TutorialPieces,
                mapResourcePath = TutorialMap
            };
            setup.players.Add(new PlayerSlot(string.IsNullOrWhiteSpace(playerName) ? "Вы" : playerName, false) { character = 0 });
            setup.players.Add(new PlayerSlot("Наставник", true, BotLevel.Smart) { character = 0 });
            return setup;
        }

        /// <summary>
        /// Правила обучения: без событий, удачи, аукциона, взяток, сделок и улучшений — только дороги, контракты,
        /// проезд, влияние и финал. Остальное объясняется в конце и в «Правилах».
        /// </summary>
        public static GameConfig TutorialConfig() => new GameConfig
        {
            RoadPiecesOverride = TutorialPieces,
            EventsEnabled = false,
            LuckEnabled = false,
            AuctionEnabled = false,
            BribesEnabled = false,
            TradesEnabled = false,
            UpgradeEnabled = false,
            PassesEnabled = false,
            FinalWarningPieces = 5
        };

        /// <summary>Настройки партии: участки из меню, правила выбранного сценария и режим (кооператив).</summary>
        public GameConfig CreateConfig()
        {
            var config = Scenarios.Apply(new GameConfig { RoadPiecesOverride = roadPieces },
                scenario >= 0 && scenario < Scenarios.All.Count ? (ScenarioKind)scenario : ScenarioKind.Standard);
            return cooperative ? config.MakeCooperative() : config;
        }

        public static readonly string[] DefaultNames = { "Аня", "Борис", "Вера", "Глеб", "Даша" };

        /// <summary>Обучение магната: учебная карта, вы и один бот, лёгкая сложность, наставник с первого раунда.</summary>
        public static GameSetup CreateMagnatePractice(GameSetup stored, string playerName)
        {
            var setup = new GameSetup
            {
                magnate = true, practice = true, forceMagnateTutorial = true, magnateTutorialMap = true, magnateDifficulty = 0,
                sound = stored?.sound ?? true, magnateLayers = stored?.magnateLayers ?? 109, magnateTutorialDone = stored?.magnateTutorialDone ?? false
            };
            setup.players.Add(new PlayerSlot(string.IsNullOrWhiteSpace(playerName) ? "Вы" : playerName, false) { character = 0 });
            setup.players.Add(new PlayerSlot("Борис", true));
            return setup;
        }

        /// <summary>Учебная партия магната: 8 раундов и по 2 фуры — короче и спокойнее обычной.</summary>
        public const int TutorialMagnateRounds = 8, TutorialMagnateTrucks = 2;

        /// <summary>Тренировочный кооператив: вы и два умных бота-союзника на карте Москвы, обычные правила, без персонажей.</summary>
        public static GameSetup CreateCoopPractice(GameSetup stored, string playerName)
        {
            var setup = new GameSetup
            {
                magnate = false, cooperative = true, practice = true, scenario = 0, roadPieces = 0, turnSeconds = 0,
                hideCardsBetweenTurns = false, sound = stored?.sound ?? true, mapResourcePath = "CityHighways/Maps/moscow"
            };
            setup.players.Add(new PlayerSlot(string.IsNullOrWhiteSpace(playerName) ? "Вы" : playerName, false) { character = 0 });
            setup.players.Add(new PlayerSlot("Борис", true) { character = 0 });
            setup.players.Add(new PlayerSlot("Даша", true) { character = 0 });
            return setup;
        }

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
        /// из тех, что никто не выбрал (если таких не хватает — любые), а «выбор из двух» передаётся
        /// в партию пометкой Characters.Draft — персонажей предложит сама игра.
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
                if (slot.character == PlayerSlot.DraftCharacter)
                {
                    result.Add(Characters.Draft);
                    continue;
                }
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
