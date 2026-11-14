using System;
using System.Linq;

namespace CityHighways.Core
{
    [Serializable]
    public class GameSaveData
    {
        public const int CurrentVersion = 10; // 2 — события, 3 — цели и удача, 4 — персонажи, 5 — история команд, 6 — сделки, 7 — влияние, 8 — аукцион, 9 — взятки, 10 — проезд при выполнении, пробки, пропуска
        /// <summary>Самая старая версия, которую ещё можно загрузить (в v2 цели и удача начнутся заново).</summary>
        public const int OldestSupportedVersion = 2;

        public int version = CurrentVersion;
        public GameConfig config;
        public PlayerSaveData[] players;
        /// <summary>Колода (последний элемент — верхняя карта), значения — Resource как int.</summary>
        public int[] drawPile;
        public int[] discardPile;
        /// <summary>Открытый ряд; -1 — пустая ячейка.</summary>
        public int[] faceUp;
        /// <summary>Колода контрактов по порядку (первый — верхний).</summary>
        public string[] contractDeck;
        public RoadOwnerSave[] owners;
        public int currentPlayer;
        public int phase;
        public int turnNumber;
        public int finalTurnsLeft = -1;
        public int consecutivePasses;

        // Городские события
        public string[] openDistricts;
        /// <summary>Оставшаяся колода событий (последний элемент открывается следующим), CityEventKind как int.</summary>
        public int[] eventDeck;
        /// <summary>Текущее событие; -1 — нет.</summary>
        public int currentEvent = -1;
        public string[] eventRoads;
        public string eventDistrict;
        public int eventNumber;
        public string[] rushHourRoads;

        // Цели мэрии и колода удачи (версия 3)
        public string[] goals;
        public GoalOwnerSave[] goalOwners;
        /// <summary>Колода удачи (последний элемент — верхняя карта), LuckCardKind как int.</summary>
        public int[] luckDeck;
        public int[] luckDiscard;
        public bool luckTaken;
        public int luckNumber;
        /// <summary>Последняя открытая карта удачи; -1 — ещё не было.</summary>
        public int lastLuck = -1;
        public int lastLuckPlayer = -1;
        public string lastLuckResult;

        // Персонажи (версия 4)
        /// <summary>Две карты на выбор Везунчика (фаза ChoosingLuck).</summary>
        public int[] luckChoice;
        public bool luckChoiceIsSecond;

        // История команд (версия 5): по ней партия загружается точь-в-точь, вместе с генератором случайных чисел.
        public GameStartInfo start;
        public GameCommand[] history;

        // Сделки (версия 6). pendingTradeFrom = -1 — предложения нет; lastTradeFrom = -1 — сделок ещё не было.
        public bool tradeUsed;
        public int tradeNumber;
        public int pendingTradeFrom = -1;
        public int pendingTradeTo;
        public int pendingGiveResource;
        public int pendingGiveCount;
        public int pendingGetResource;
        public int pendingGetCount;
        public int lastTradeFrom = -1;
        public int lastTradeTo;
        public int lastGiveResource;
        public int lastGiveCount;
        public int lastGetResource;
        public int lastGetCount;
        public bool lastTradeAccepted;

        // Влияние (версия 7)
        public bool influenceUsed;
        public bool permitActive;
        public int pendingGiveInfluence;
        public int pendingGetInfluence;
        public int lastGiveInfluence;
        public int lastGetInfluence;

        // Аукцион мэрии (версия 8)
        public bool auctionHeld;
        public string auctionContract;
        public int[] bids;
        public int auctionStarter;
        public int auctionNumber;
        public string lastAuctionContract;
        public int lastAuctionWinner = -1;
        public int lastAuctionPrice;
        public int[] lastAuctionBids;

        // Взятки (версия 9): нераскрытые взятки — игрок и вид, по парам.
        public int[] bribePlayers;
        public int[] bribeKinds;
        public bool bribedThisTurn;
        public bool accusedThisTurn;
        public bool discountActive;
        public int whisperNumber;
        public int accusationNumber;
        public int lastAccuser = -1;
        public int lastAccused;
        public bool lastAccusationCorrect;
        public int lastAccusationExposed;
        public int turnHandStart;
        public bool turnPaidLess;
        public bool[] suspicious;

        // Пропуска в сделках (версия 10)
        public bool pendingGivePass;
        public bool pendingGetPass;
        public bool lastGivePass;
        public bool lastGetPass;
    }

    /// <summary>Оплаченный маршрут выполненного контракта (версия 10): путь и проезды по чужим дорогам.</summary>
    [Serializable]
    public class PaidRouteSave
    {
        public string contract;
        public string[] path;
        public string[] roads;
        public int[] owners;
        public int[] tolls;
        public int[] jamFees;
    }

    [Serializable]
    public class GoalOwnerSave
    {
        public string goal;
        public int player;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public string name;
        public int roadPieces;
        public int roadPoints;
        public int goalPoints;
        public int luckPoints;
        public int luckBuffs;
        /// <summary>CharacterKind как int; 0 — без персонажа.</summary>
        public int character;
        public int freeRoads;
        public int luckDebuffs;
        /// <summary>Число карт каждого вида, индекс — Resource.</summary>
        public int[] hand;
        public string[] roads;
        public string[] contracts;
        public string[] pendingContracts;
        // Влияние (версия 7)
        public int influence;
        public int influenceEarned;
        public int influenceSpent;
        public string[] rewardedContracts;
        public string[] influenceDistricts;
        public string mayorContract;
        public int penaltyPoints;
        public int bribesGiven;
        public int bribesExposed;
        // Проезд и пропуска (версия 10)
        public int tollsPaid;
        public int tollsEarned;
        public int jamFees;
        public PaidRouteSave[] paidRoutes;
        public int[] passesFrom;
    }

    [Serializable]
    public class RoadOwnerSave
    {
        public string road;
        public int player;
    }

    public sealed partial class Game
    {
        /// <summary>Конструктор для восстановления: только каркас, состояние заполняет FromSave.</summary>
        Game(CityMap map, GameConfig config, ResourceDeck deck, ContractDeck contracts, Random rng)
        {
            Map = map;
            Config = config;
            _rng = rng;
            _deck = deck;
            _contractDeck = contracts;
            _faceUp = new Resource?[config.FaceUpSlots];
        }

        public GameSaveData CreateSave()
        {
            return new GameSaveData
            {
                config = Config,
                players = _players.Select(p => new PlayerSaveData
                {
                    name = p.Name,
                    roadPieces = p.RoadPieces,
                    roadPoints = p.RoadPoints,
                    goalPoints = p.GoalPoints,
                    luckPoints = p.LuckPoints,
                    luckBuffs = p.LuckBuffs,
                    character = (int)p.Character,
                    freeRoads = p.FreeRoadsLeft,
                    luckDebuffs = p.LuckDebuffs,
                    hand = Enumerable.Range(0, ResourceInfo.Count).Select(r => p.CardCount((Resource)r)).ToArray(),
                    roads = p.Roads.Select(r => r.Id).ToArray(),
                    contracts = p.Contracts.Select(c => c.Id).ToArray(),
                    pendingContracts = p.PendingContracts.Select(c => c.Id).ToArray(),
                    influence = p.Influence,
                    influenceEarned = p.InfluenceEarned,
                    influenceSpent = p.InfluenceSpent,
                    rewardedContracts = p.RewardedContracts.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                    influenceDistricts = p.InfluenceDistricts.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                    mayorContract = p.MayorContractId,
                    penaltyPoints = p.PenaltyPoints,
                    bribesGiven = p.BribesGiven,
                    bribesExposed = p.BribesExposed,
                    tollsPaid = p.TollsPaid,
                    tollsEarned = p.TollsEarned,
                    jamFees = p.JamFees,
                    paidRoutes = p.PaidRoutes.Values.OrderBy(r => r.ContractId, StringComparer.Ordinal).Select(r => new PaidRouteSave
                    {
                        contract = r.ContractId,
                        path = r.Path.ToArray(),
                        roads = r.Passages.Select(x => x.RoadId).ToArray(),
                        owners = r.Passages.Select(x => x.Owner).ToArray(),
                        tolls = r.Passages.Select(x => x.Toll).ToArray(),
                        jamFees = r.Passages.Select(x => x.JamFee).ToArray()
                    }).ToArray(),
                    passesFrom = p.PassesFrom.OrderBy(i => i).ToArray()
                }).ToArray(),
                drawPile = _deck.DrawPile.Select(c => (int)c).ToArray(),
                discardPile = _deck.DiscardPile.Select(c => (int)c).ToArray(),
                faceUp = _faceUp.Select(c => c.HasValue ? (int)c.Value : -1).ToArray(),
                contractDeck = _contractDeck.Cards.Select(c => c.Id).ToArray(),
                owners = _roadOwners.Select(pair => new RoadOwnerSave { road = pair.Key, player = pair.Value }).ToArray(),
                currentPlayer = CurrentPlayerIndex,
                phase = (int)Phase,
                turnNumber = TurnNumber,
                finalTurnsLeft = _finalTurnsLeft,
                consecutivePasses = _consecutivePasses,
                openDistricts = _openDistricts.OrderBy(d => d, StringComparer.Ordinal).ToArray(),
                eventDeck = _eventDeck.Select(e => (int)e).ToArray(),
                currentEvent = CurrentEvent != null ? (int)CurrentEvent.Kind : -1,
                eventRoads = CurrentEvent != null ? CurrentEvent.ClosedRoads.ToArray() : new string[0],
                eventDistrict = CurrentEvent?.DistrictId,
                eventNumber = EventNumber,
                rushHourRoads = _rushHourRoads.OrderBy(r => r, StringComparer.Ordinal).ToArray(),
                goals = _goals.Select(g => g.Id).ToArray(),
                goalOwners = _goalOwners.Select(pair => new GoalOwnerSave { goal = pair.Key, player = pair.Value }).ToArray(),
                luckDeck = _luckDeck.Select(k => (int)k).ToArray(),
                luckDiscard = _luckDiscard.Select(k => (int)k).ToArray(),
                luckTaken = _luckTakenThisTurn,
                luckNumber = LuckNumber,
                lastLuck = LastLuckCard.HasValue ? (int)LastLuckCard.Value : -1,
                lastLuckPlayer = LastLuckPlayer,
                lastLuckResult = LastLuckResult,
                luckChoice = _luckChoice.Select(k => (int)k).ToArray(),
                luckChoiceIsSecond = _luckChoiceIsSecondCard,
                start = StartInfo,
                history = _history.ToArray(),
                tradeUsed = _tradeUsedThisTurn,
                tradeNumber = TradeNumber,
                pendingTradeFrom = PendingTrade?.From ?? -1,
                pendingTradeTo = PendingTrade?.To ?? 0,
                pendingGiveResource = PendingTrade != null ? (int)PendingTrade.GiveResource : 0,
                pendingGiveCount = PendingTrade?.GiveCount ?? 0,
                pendingGetResource = PendingTrade != null ? (int)PendingTrade.GetResource : 0,
                pendingGetCount = PendingTrade?.GetCount ?? 0,
                lastTradeFrom = LastTrade?.From ?? -1,
                lastTradeTo = LastTrade?.To ?? 0,
                lastGiveResource = LastTrade != null ? (int)LastTrade.GiveResource : 0,
                lastGiveCount = LastTrade?.GiveCount ?? 0,
                lastGetResource = LastTrade != null ? (int)LastTrade.GetResource : 0,
                lastGetCount = LastTrade?.GetCount ?? 0,
                lastTradeAccepted = LastTradeAccepted,
                influenceUsed = _influenceUsedThisTurn,
                permitActive = _permitActive,
                pendingGiveInfluence = PendingTrade?.GiveInfluence ?? 0,
                pendingGetInfluence = PendingTrade?.GetInfluence ?? 0,
                lastGiveInfluence = LastTrade?.GiveInfluence ?? 0,
                lastGetInfluence = LastTrade?.GetInfluence ?? 0,
                pendingGivePass = PendingTrade?.GivePass ?? false,
                pendingGetPass = PendingTrade?.GetPass ?? false,
                lastGivePass = LastTrade?.GivePass ?? false,
                lastGetPass = LastTrade?.GetPass ?? false,
                auctionHeld = _auctionHeld,
                auctionContract = _auctionContract?.Id,
                bids = _bids?.ToArray(),
                auctionStarter = _auctionStarter,
                auctionNumber = AuctionNumber,
                lastAuctionContract = LastAuction?.Contract.Id,
                lastAuctionWinner = LastAuction?.Winner ?? -1,
                lastAuctionPrice = LastAuction?.Price ?? 0,
                lastAuctionBids = LastAuction?.Bids.ToArray(),
                bribePlayers = _bribes.Select(b => b.Player).ToArray(),
                bribeKinds = _bribes.Select(b => (int)b.Kind).ToArray(),
                bribedThisTurn = _bribedThisTurn,
                accusedThisTurn = _accusedThisTurn,
                discountActive = _discountActive,
                whisperNumber = WhisperNumber,
                accusationNumber = AccusationNumber,
                lastAccuser = LastAccusation?.Accuser ?? -1,
                lastAccused = LastAccusation?.Accused ?? 0,
                lastAccusationCorrect = LastAccusation?.Correct ?? false,
                lastAccusationExposed = LastAccusation?.Exposed ?? 0,
                turnHandStart = _turnHandStart,
                turnPaidLess = _turnPaidLess,
                suspicious = _suspicious?.ToArray()
            };
        }

        /// <summary>Восстанавливает партию. FormatException — если сохранение не подходит к карте.</summary>
        /// <summary>
        /// Загрузить партию как можно точнее: если в сохранении есть история команд, партия воспроизводится
        /// с начала — так восстанавливается и генератор случайных чисел, и дальше она идёт ровно так же,
        /// как шла бы без сохранения. Если воспроизвести не вышло (старое сохранение, другая версия карты
        /// или правил) — загружается снимок через FromSave.
        /// </summary>
        public static Game Load(CityMap map, GameSaveData data)
        {
            if (data != null && data.version >= 5 && data.start != null && data.start.players != null && data.start.players.Length > 0)
            {
                try
                {
                    var replayed = Replay(map, data.start, data.history);
                    if (replayed.SameStateAs(data)) return replayed;
                }
                catch (Exception)
                {
                    // Воспроизвести не удалось — ниже загрузим снимок.
                }
            }
            return FromSave(map, data);
        }

        /// <summary>Совпадает ли состояние с сохранённым снимком (сравниваются ключевые части).</summary>
        bool SameStateAs(GameSaveData data)
        {
            var mine = CreateSave();
            return mine.phase == data.phase
                   && mine.currentPlayer == data.currentPlayer
                   && mine.turnNumber == data.turnNumber
                   && Same(mine.drawPile, data.drawPile) && Same(mine.discardPile, data.discardPile)
                   && Same(mine.faceUp, data.faceUp)
                   && Same(mine.contractDeck, data.contractDeck)
                   && Same(mine.luckDeck, data.luckDeck)
                   && mine.players.Length == data.players.Length
                   && mine.players.Zip(data.players, (a, b) => a.roadPoints == b.roadPoints && a.roadPieces == b.roadPieces
                                                              && Same(a.hand, b.hand) && Same(a.roads, b.roads)
                                                              && Same(a.contracts, b.contracts)
                                                              && (data.version < 7 || a.influence == b.influence)
                                                              && (data.version < 10 || (a.tollsPaid == b.tollsPaid && a.tollsEarned == b.tollsEarned))).All(x => x);
        }

        static bool Same<T>(T[] a, T[] b) => (a ?? new T[0]).SequenceEqual(b ?? new T[0]);

        public static Game FromSave(CityMap map, GameSaveData data, int? seed = null)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.version < GameSaveData.OldestSupportedVersion || data.version > GameSaveData.CurrentVersion)
                throw new FormatException($"Сохранение версии {data.version} не поддерживается.");

            var config = data.config ?? new GameConfig();
            var errors = map.Validate(config.MaxRoadLength);
            if (errors.Count > 0) throw new FormatException("Карта содержит ошибки:\n" + string.Join("\n", errors));

            var players = data.players ?? new PlayerSaveData[0];
            if (players.Length < config.MinPlayers || players.Length > config.MaxPlayers)
                throw new FormatException("В сохранении неверное число игроков.");
            if ((GamePhase)data.phase == GamePhase.GameOver) throw new FormatException("Эта партия уже закончена.");

            Contract FindContract(string id) =>
                map.GetContract(id) ?? throw new FormatException($"В карте нет контракта {id}.");
            Road FindRoad(string id) =>
                map.GetRoad(id) ?? throw new FormatException($"В карте нет дороги {id}.");
            Resource ToResource(int value)
            {
                if (value < 0 || value >= ResourceInfo.Count) throw new FormatException($"Неизвестный ресурс {value}.");
                return (Resource)value;
            }

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var deck = ResourceDeck.Restore(
                (data.drawPile ?? new int[0]).Select(ToResource),
                (data.discardPile ?? new int[0]).Select(ToResource),
                rng);
            var contracts = ContractDeck.Restore((data.contractDeck ?? new string[0]).Select(FindContract));
            var game = new Game(map, config, deck, contracts, rng);

            for (int i = 0; i < players.Length; i++)
            {
                var saved = players[i];
                var player = new PlayerState(i, saved.name ?? "Игрок " + (i + 1), saved.roadPieces);
                player.RoadPoints = saved.roadPoints;
                player.GoalPoints = saved.goalPoints;
                player.LuckPoints = saved.luckPoints;
                player.LuckBuffs = saved.luckBuffs;
                if (saved.character < 0 || saved.character > (int)CharacterKind.Bridger)
                    throw new FormatException($"Неизвестный персонаж {saved.character}.");
                player.Character = (CharacterKind)saved.character;
                player.FreeRoadsLeft = saved.freeRoads;
                player.LuckDebuffs = saved.luckDebuffs;
                var hand = saved.hand ?? new int[0];
                for (int r = 0; r < hand.Length && r < ResourceInfo.Count; r++)
                    if (hand[r] > 0) player.AddCard((Resource)r, hand[r]);
                foreach (var id in saved.roads ?? new string[0]) player.AddRoad(FindRoad(id));
                foreach (var id in saved.contracts ?? new string[0]) player.ContractList.Add(FindContract(id));
                foreach (var id in saved.pendingContracts ?? new string[0]) player.PendingList.Add(FindContract(id));
                player.Influence = saved.influence;
                player.InfluenceEarned = saved.influenceEarned;
                player.InfluenceSpent = saved.influenceSpent;
                foreach (var id in saved.rewardedContracts ?? new string[0]) player.RewardedContracts.Add(id);
                foreach (var id in saved.influenceDistricts ?? new string[0]) player.InfluenceDistricts.Add(id);
                player.MayorContractId = string.IsNullOrEmpty(saved.mayorContract) ? null : saved.mayorContract;
                player.PenaltyPoints = saved.penaltyPoints;
                player.BribesGiven = saved.bribesGiven;
                player.BribesExposed = saved.bribesExposed;
                player.TollsPaid = saved.tollsPaid;
                player.TollsEarned = saved.tollsEarned;
                player.JamFees = saved.jamFees;
                foreach (var route in saved.paidRoutes ?? new PaidRouteSave[0])
                {
                    var contract = FindContract(route.contract);
                    int n = route.roads?.Length ?? 0;
                    if (route.owners == null || route.tolls == null || route.jamFees == null
                        || route.owners.Length != n || route.tolls.Length != n || route.jamFees.Length != n)
                        throw new FormatException($"Сохранение: неверный маршрут контракта {route.contract}.");
                    foreach (var id in route.path ?? new string[0]) FindRoad(id);
                    var passages = new System.Collections.Generic.List<PaidPassage>();
                    for (int k = 0; k < n; k++)
                    {
                        FindRoad(route.roads[k]);
                        if (route.owners[k] < -1 || route.owners[k] >= players.Length)
                            throw new FormatException($"Сохранение: неверный владелец дороги {route.roads[k]}.");
                        passages.Add(new PaidPassage(route.roads[k], route.owners[k], route.tolls[k], route.jamFees[k]));
                    }
                    player.PaidRoutes[contract.Id] = new PaidRoute(contract.Id, (route.path ?? new string[0]).ToList(), passages);
                }
                foreach (int owner in saved.passesFrom ?? new int[0])
                {
                    if (owner < 0 || owner >= players.Length || owner == i) throw new FormatException("Сохранение: неверный пропуск.");
                    player.PassesFrom.Add(owner);
                }
                game._players.Add(player);
            }

            var faceUp = data.faceUp ?? new int[0];
            for (int i = 0; i < game._faceUp.Length; i++)
                game._faceUp[i] = i < faceUp.Length && faceUp[i] >= 0 ? ToResource(faceUp[i]) : (Resource?)null;

            foreach (var owner in data.owners ?? new RoadOwnerSave[0])
            {
                FindRoad(owner.road);
                if (owner.player < 0 || owner.player >= players.Length)
                    throw new FormatException($"У дороги {owner.road} неверный владелец.");
                game._roadOwners[owner.road] = owner.player;
            }

            if (data.currentPlayer < 0 || data.currentPlayer >= players.Length)
                throw new FormatException("В сохранении неверный текущий игрок.");
            game.CurrentPlayerIndex = data.currentPlayer;
            game.Phase = (GamePhase)data.phase;
            game.TurnNumber = data.turnNumber;
            game._finalTurnsLeft = data.finalTurnsLeft;
            game._consecutivePasses = data.consecutivePasses;

            // События: открытые районы, колода, текущее событие и его участки, магистрали часа пик.
            if (data.openDistricts != null && data.openDistricts.Length > 0)
            {
                foreach (var id in data.openDistricts)
                {
                    if (map.GetDistrict(id) == null) throw new FormatException($"В карте нет района {id}.");
                    game._openDistricts.Add(id);
                }
            }
            else
            {
                foreach (var district in map.Districts)
                    if (!district.IsExpansion) game._openDistricts.Add(district.Id);
            }
            foreach (int kind in data.eventDeck ?? new int[0])
            {
                if (kind < 0 || kind > (int)CityEventKind.Investor) throw new FormatException($"Неизвестное событие {kind}.");
                game._eventDeck.Add((CityEventKind)kind);
            }
            foreach (var id in data.rushHourRoads ?? new string[0]) game._rushHourRoads.Add(FindRoad(id).Id);
            if (data.currentEvent >= 0)
            {
                if (data.currentEvent > (int)CityEventKind.Investor) throw new FormatException($"Неизвестное событие {data.currentEvent}.");
                var roads = (data.eventRoads ?? new string[0]).Select(id => FindRoad(id).Id).ToList();
                game.CurrentEvent = new CityEvent((CityEventKind)data.currentEvent, roads,
                    string.IsNullOrEmpty(data.eventDistrict) ? null : data.eventDistrict);
                if (game.CurrentEvent.Kind == CityEventKind.Repair)
                    foreach (var id in roads) game._repairRoads.Add(id);
            }
            game.EventNumber = data.eventNumber;

            if (data.version < 3)
            {
                // В старой партии не было целей и удачи — открываем их сейчас.
                game.SetupGoalsAndLuck();
                return game;
            }

            LuckCardKind ToLuck(int value)
            {
                if (value < 0 || value >= LuckInfo.KindCount) throw new FormatException($"Неизвестная карта удачи {value}.");
                return (LuckCardKind)value;
            }
            foreach (var id in data.goals ?? new string[0])
                game._goals.Add(map.GetGoal(id) ?? throw new FormatException($"В карте нет цели {id}."));
            foreach (var owner in data.goalOwners ?? new GoalOwnerSave[0])
            {
                if (game._goals.All(g => g.Id != owner.goal)) throw new FormatException($"Цель {owner.goal} не из этой партии.");
                if (owner.player < 0 || owner.player >= players.Length)
                    throw new FormatException($"У цели {owner.goal} неверный владелец.");
                game._goalOwners[owner.goal] = owner.player;
            }
            game._luckDeck.AddRange((data.luckDeck ?? new int[0]).Select(ToLuck));
            game._luckDiscard.AddRange((data.luckDiscard ?? new int[0]).Select(ToLuck));
            game._luckTakenThisTurn = data.luckTaken;
            game.LuckNumber = data.luckNumber;
            game.LastLuckCard = data.lastLuck >= 0 ? ToLuck(data.lastLuck) : (LuckCardKind?)null;
            game.LastLuckPlayer = data.lastLuckPlayer;
            game.LastLuckResult = data.lastLuckResult;
            game._luckChoice.AddRange((data.luckChoice ?? new int[0]).Select(ToLuck));
            game._luckChoiceIsSecondCard = data.luckChoiceIsSecond;
            game.StartInfo = data.version >= 5 && data.start != null && data.start.players != null && data.start.players.Length > 0 ? data.start : null;
            game._history.AddRange(data.history ?? new GameCommand[0]);

            TradeOffer ReadTrade(int from, int to, int give, int giveCount, int get, int getCount, int giveInfluence, int getInfluence,
                bool givePass, bool getPass)
            {
                if (from < 0) return null;
                if (from >= players.Length || to < 0 || to >= players.Length || to == from
                    || give < 0 || give >= ResourceInfo.Count || get < 0 || get >= ResourceInfo.Count)
                    throw new FormatException("Сохранение: неверная сделка.");
                return new TradeOffer(from, to, (Resource)give, giveCount, (Resource)get, getCount, giveInfluence, getInfluence, givePass, getPass);
            }
            game._tradeUsedThisTurn = data.tradeUsed;
            game.TradeNumber = data.tradeNumber;
            game.PendingTrade = ReadTrade(data.pendingTradeFrom, data.pendingTradeTo, data.pendingGiveResource,
                data.pendingGiveCount, data.pendingGetResource, data.pendingGetCount, data.pendingGiveInfluence, data.pendingGetInfluence,
                data.pendingGivePass, data.pendingGetPass);
            game.LastTrade = ReadTrade(data.lastTradeFrom, data.lastTradeTo, data.lastGiveResource,
                data.lastGiveCount, data.lastGetResource, data.lastGetCount, data.lastGiveInfluence, data.lastGetInfluence,
                data.lastGivePass, data.lastGetPass);
            game._influenceUsedThisTurn = data.influenceUsed;
            game._permitActive = data.permitActive;
            game._auctionHeld = data.auctionHeld;
            game.AuctionNumber = data.auctionNumber;
            game._auctionStarter = data.auctionStarter;
            if (game.Phase == GamePhase.Bidding)
            {
                if (string.IsNullOrEmpty(data.auctionContract) || data.bids == null || data.bids.Length != players.Length)
                    throw new FormatException("Сохранение: аукцион без контракта или ставок.");
                game._auctionContract = FindContract(data.auctionContract);
                game._bids = data.bids.ToArray();
            }
            var bribePlayers = data.bribePlayers ?? new int[0];
            var bribeKinds = data.bribeKinds ?? new int[0];
            if (bribePlayers.Length != bribeKinds.Length) throw new FormatException("Сохранение: неверные взятки.");
            for (int i = 0; i < bribePlayers.Length; i++)
            {
                if (bribePlayers[i] < 0 || bribePlayers[i] >= players.Length || bribeKinds[i] < 0 || bribeKinds[i] > (int)BribeKind.ExtraCard)
                    throw new FormatException("Сохранение: неверная взятка.");
                game._bribes.Add(new BribeRecord { Player = bribePlayers[i], Kind = (BribeKind)bribeKinds[i] });
            }
            game._bribedThisTurn = data.bribedThisTurn;
            game._accusedThisTurn = data.accusedThisTurn;
            game._discountActive = data.discountActive;
            game.WhisperNumber = data.whisperNumber;
            game.AccusationNumber = data.accusationNumber;
            if (data.lastAccuser >= 0)
                game.LastAccusation = new AccusationResult(data.lastAccuser, data.lastAccused, data.lastAccusationCorrect, data.lastAccusationExposed);
            game._turnHandStart = data.turnHandStart;
            game._turnPaidLess = data.turnPaidLess;
            game._suspicious = data.suspicious != null && data.suspicious.Length == players.Length ? data.suspicious.ToArray() : null;
            if (!string.IsNullOrEmpty(data.lastAuctionContract))
                game.LastAuction = new AuctionResult(FindContract(data.lastAuctionContract), data.lastAuctionWinner,
                    data.lastAuctionPrice, (data.lastAuctionBids ?? new int[0]).ToArray());
            if (data.version < 7)
            {
                // Старая партия: влияние начинается с нуля, уже выполненное и освоенное за прошлое не начисляется.
                foreach (var player in game._players)
                {
                    foreach (var road in player.Roads)
                    {
                        player.InfluenceDistricts.Add(map.DistrictOf(road.A));
                        player.InfluenceDistricts.Add(map.DistrictOf(road.B));
                    }
                    foreach (var contract in player.Contracts)
                        if (game.CheckContract(player.Index, contract).Completed) player.RewardedContracts.Add(contract.Id);
                }
            }
            game.LastTradeAccepted = data.lastTradeAccepted;
            if (game.Phase == GamePhase.RespondingTrade && game.PendingTrade == null)
                throw new FormatException("Сохранение: ответ на сделку без сделки.");
            if (game.Phase == GamePhase.ChoosingLuck && game._luckChoice.Count == 0)
                throw new FormatException("Сохранение: выбор карты удачи без карт.");
            game.RebuildTraffic();
            // Старая партия: уже выполненные контракты платят проезд сейчас, по нынешним ценам.
            if (data.version < 10 && game.Phase != GamePhase.GameOver) game.SettleContracts(quiet: true);
            return game;
        }
    }
}
