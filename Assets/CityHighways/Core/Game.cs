using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>
    /// Правила City Highways. Не знает про Unity: получает команды (взять карту, построить дорогу,
    /// взять контракты) и меняет состояние. При ошибке команда возвращает причину и ничего не меняет.
    /// </summary>
    public sealed partial class Game
    {
        readonly List<PlayerState> _players = new List<PlayerState>();
        readonly Resource?[] _faceUp;
        readonly Dictionary<string, int> _roadOwners = new Dictionary<string, int>();
        readonly ResourceDeck _deck;
        readonly ContractDeck _contractDeck;
        readonly Random _rng;

        // Городские события
        readonly HashSet<string> _openDistricts = new HashSet<string>();
        readonly List<CityEventKind> _eventDeck = new List<CityEventKind>();
        readonly HashSet<string> _repairRoads = new HashSet<string>();
        readonly HashSet<string> _rushHourRoads = new HashSet<string>();

        int _finalTurnsLeft = -1; // -1 — последний круг ещё не начался
        int _consecutivePasses;

        public CityMap Map { get; }
        public GameConfig Config { get; }
        public IReadOnlyList<PlayerState> Players => _players;
        public int CurrentPlayerIndex { get; private set; }
        public PlayerState CurrentPlayer => _players[CurrentPlayerIndex];
        public GamePhase Phase { get; private set; }
        public int TurnNumber { get; private set; }

        /// <summary>Открытый ряд. null — пустая ячейка (если карты кончились).</summary>
        public IReadOnlyList<Resource?> FaceUpCards => _faceUp;
        public int DeckCount => _deck.DrawPileCount;
        public int DiscardCount => _deck.DiscardPileCount;
        public int ContractDeckCount => _contractDeck.Count;
        public bool IsFinalRound => _finalTurnsLeft > 0;
        public GameResult Result { get; private set; }

        /// <summary>Событие текущего круга (null — события выключены или партия ещё не началась).</summary>
        public CityEvent CurrentEvent { get; private set; }
        /// <summary>Сколько событий уже открылось; интерфейс по нему замечает новое событие.</summary>
        public int EventNumber { get; private set; }
        public int EventDeckCount => _eventDeck.Count;

        /// <summary>Журнал событий для консоли и интерфейса.</summary>
        public event Action<string> Log;
        /// <summary>Вызывается после каждого успешного действия.</summary>
        public event Action StateChanged;

        public Game(CityMap map, IList<string> playerNames, GameConfig config = null, int? seed = null,
            IList<CharacterKind> characters = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Config = config ?? new GameConfig();

            if (playerNames == null || playerNames.Count < Config.MinPlayers || playerNames.Count > Config.MaxPlayers)
                throw new ArgumentException($"Нужно от {Config.MinPlayers} до {Config.MaxPlayers} игроков.");

            var errors = map.Validate(Config.MaxRoadLength);
            if (errors.Count > 0) throw new ArgumentException("Карта содержит ошибки:\n" + string.Join("\n", errors));

            Seed = seed ?? new Random().Next();
            _rng = new Random(Seed);
            foreach (var district in map.Districts)
                if (!district.IsExpansion) _openDistricts.Add(district.Id);

            _deck = new ResourceDeck(BuildCardSet(Config), _rng);
            // В колоде только контракты открытых районов; контракты расширений придут с событием.
            _contractDeck = new ContractDeck(map.Contracts.Where(IsContractOpen), _rng);
            _faceUp = new Resource?[Config.FaceUpSlots];

            int pieces = Config.RoadPiecesFor(playerNames.Count);
            StartInfo = new GameStartInfo
            {
                players = playerNames.ToArray(),
                characters = (characters ?? new CharacterKind[playerNames.Count]).Select(c => (int)c).ToArray(),
                seed = Seed,
                config = Config
            };
            if (characters != null && characters.Count != playerNames.Count)
                throw new ArgumentException("Персонажей должно быть столько же, сколько игроков.");
            for (int i = 0; i < playerNames.Count; i++)
            {
                var player = new PlayerState(i, playerNames[i], pieces);
                player.Character = characters?[i] ?? CharacterKind.None;
                if (player.Character == CharacterKind.Foreman) player.FreeRoadsLeft = Config.ForemanFreeRoads;
                _players.Add(player);
            }

            foreach (var player in _players)
            {
                for (int k = 0; k < Config.StartingHandSize; k++)
                    if (_deck.TryDraw(out var card)) player.AddCard(card);
                player.PendingList.AddRange(_contractDeck.Draw(Config.InitialContractsDealt));
            }

            SetupGoalsAndLuck();
            RefillFaceUpRow();
            Phase = GamePhase.SetupContracts;
            CheckSetupComplete();
        }

        // ================================================================== Запросы

        public int? GetRoadOwner(string roadId) =>
            roadId != null && _roadOwners.TryGetValue(roadId, out var owner) ? owner : (int?)null;

        /// <summary>Игрок, от которого ждут действия; на старте — первый, кто не выбрал контракты; после конца — -1.</summary>
        public int ActingPlayerIndex
        {
            get
            {
                if (Phase == GamePhase.GameOver) return -1;
                if (Phase == GamePhase.RespondingTrade && PendingTrade != null) return PendingTrade.To;
                if (Phase == GamePhase.SetupContracts)
                {
                    foreach (var p in _players)
                        if (p.PendingContracts.Count > 0) return p.Index;
                    return -1;
                }
                return CurrentPlayerIndex;
            }
        }

        public bool CanDrawAnyCard() => _deck.AvailableCount > 0 || _faceUp.Any(c => c.HasValue);

        /// <summary>Выполнен ли контракт игрока прямо сейчас и сколько чужих дорог на пути (для интерфейса и ботов).</summary>
        public ContractCheck CheckContract(int playerIndex, Contract contract) =>
            Scoring.CheckContract(Map, contract, playerIndex, GetRoadOwner, Config.MaxForeignRoadsPerContract, TollFor);

        // ---------------------------------------------------------------- Карта с учётом событий

        public bool IsDistrictOpen(string districtId) => districtId != null && _openDistricts.Contains(districtId);

        public bool IsLocationOpen(string locationId) => IsDistrictOpen(Map.DistrictOf(locationId));

        /// <summary>Дорога есть на карте: оба её конца в открытых районах.</summary>
        public bool IsRoadOpen(string roadId)
        {
            var road = Map.GetRoad(roadId);
            return road != null && IsLocationOpen(road.A) && IsLocationOpen(road.B);
        }

        bool IsContractOpen(Contract contract) => contract.Districts.All(IsDistrictOpen);

        /// <summary>Участок закрыт на ремонт до конца круга.</summary>
        public bool IsUnderRepair(string roadId) => _repairRoads.Contains(roadId);

        /// <summary>Дорога построена в «Час пик» — проезд по ней дороже.</summary>
        public bool IsRushHourRoad(string roadId) => _rushHourRoads.Contains(roadId);

        /// <summary>Сколько очков стоит проехать по этой (чужой) дороге.</summary>
        /// <summary>Плата за проезд: у Сборщика пошлин своя ставка, у магистралей часа пик — вдвое.</summary>
        public int TollFor(string roadId)
        {
            var owner = GetRoadOwner(roadId);
            int baseToll = owner.HasValue && _players[owner.Value].Character == CharacterKind.Tollman
                ? Config.TollmanToll
                : Config.TollPerRoad;
            return baseToll * (_rushHourRoads.Contains(roadId) ? Config.RushHourTollMultiplier : 1);
        }

        /// <summary>Сколько карт стоит дорога текущему игроку. Участков уходит по длине.</summary>
        public int BuildCost(Road road) => BuildCost(road, CurrentPlayer);

        /// <summary>
        /// Сколько карт стоит дорога игроку: мосты дешевеют от «Субсидии мэрии» и у Мостовика
        /// (скидки складываются, но мост стоит хотя бы 1 карту).
        /// </summary>
        /// <summary>Закрыта ли дорога Парадом (касается района ParadeDistrict).</summary>
        public bool IsParadeClosed(string roadId)
        {
            if (CurrentEvent == null || CurrentEvent.Kind != CityEventKind.Parade) return false;
            var road = Map.GetRoad(roadId);
            return road != null && (Map.DistrictOf(road.A) == Config.ParadeDistrict || Map.DistrictOf(road.B) == Config.ParadeDistrict);
        }

        public int BuildCost(Road road, PlayerState player)
        {
            bool permit = _permitActive && player != null && player.Index == CurrentPlayerIndex;
            if (road.Resource == Resource.Gravel && CurrentEvent != null && CurrentEvent.Kind == CityEventKind.Snow && !permit)
                return road.Length + Config.SnowExtraCost;
            if (!road.IsBridge) return road.Length;
            int cost = road.Length;
            if (CurrentEvent != null && CurrentEvent.Kind == CityEventKind.Subsidy) cost -= Config.BridgeSubsidy;
            if (player != null && player.Character == CharacterKind.Bridger) cost -= Config.BridgerDiscount;
            return Math.Max(1, cost);
        }

        /// <summary>Все способы, которыми текущий игрок может оплатить дорогу прямо сейчас.</summary>
        public IReadOnlyList<PaymentOption> GetPaymentOptions(string roadId)
        {
            var options = new List<PaymentOption>();
            if (Phase != GamePhase.AwaitingAction) return options;

            var road = Map.GetRoad(roadId);
            var player = CurrentPlayer;
            if (road == null || CheckRoadAvailable(player, road) != null) return options;

            int machinery = player.CardCount(Resource.Machinery);
            IEnumerable<Resource> resources = road.Resource.HasValue
                ? new[] { road.Resource.Value }
                : (IEnumerable<Resource>)ResourceInfo.Building;

            int cost = BuildCost(road);
            foreach (var resource in resources)
            {
                int have = player.CardCount(resource);
                if (have == 0) continue;
                int needMachinery = cost - Math.Min(have, cost);
                if (needMachinery <= machinery) options.Add(new PaymentOption(resource, needMachinery));
            }
            if (machinery >= cost) options.Add(new PaymentOption(Resource.Machinery, cost));
            return options;
        }

        /// <summary>Почему текущий игрок не может построить дорогу сейчас; null — может.</summary>
        public string GetBuildBlocker(string roadId)
        {
            if (Phase != GamePhase.AwaitingAction) return "Сейчас нельзя строить.";
            var road = Map.GetRoad(roadId);
            if (road == null) return "Нет такой дороги.";
            var reason = CheckRoadAvailable(CurrentPlayer, road);
            if (reason != null) return reason;
            if (GetPaymentOptions(roadId).Count == 0)
            {
                int cost = BuildCost(road);
                string need = road.Resource.HasValue
                    ? $"{cost} карт(ы) «{ResourceInfo.RussianName(road.Resource.Value)}»"
                    : $"{cost} карт(ы) одного ресурса";
                return $"Нужно {need} (техника подходит вместо любой карты).";
            }
            return null;
        }

        /// <summary>Есть ли у текущего игрока хоть одно действие, кроме паса.</summary>
        public bool HasAnyLegalAction()
        {
            if (Phase != GamePhase.AwaitingAction) return true;
            if (CanDrawAnyCard() || _contractDeck.Count > 0) return true;
            return Map.Roads.Any(r => GetPaymentOptions(r.Id).Count > 0);
        }

        // ================================================================== Действия

        ActionResult DoKeepInitialContracts(int playerIndex, IList<string> contractIds)
        {
            if (Phase != GamePhase.SetupContracts) return ActionResult.Fail("Стартовые контракты уже выбраны.");
            if (playerIndex < 0 || playerIndex >= _players.Count) return ActionResult.Fail("Нет такого игрока.");

            var player = _players[playerIndex];
            if (player.PendingContracts.Count == 0) return ActionResult.Fail($"{player.Name} уже выбрал контракты.");

            var check = ResolveKept(player, contractIds, Config.InitialContractsMinKeep, out var kept);
            if (!check.Success) return check;

            ApplyKept(player, kept);
            Emit($"{player.Name} берёт стартовых контрактов: {kept.Count}.");
            CheckSetupComplete();
            Changed();
            return ActionResult.Ok();
        }

        ActionResult DoDrawFaceUpCard(int slot)
        {
            if (Phase != GamePhase.AwaitingAction && Phase != GamePhase.DrawingSecondCard)
                return ActionResult.Fail("Сейчас нельзя брать карты.");
            if (slot < 0 || slot >= _faceUp.Length || !_faceUp[slot].HasValue)
                return ActionResult.Fail("В этой ячейке нет карты.");

            var card = _faceUp[slot].Value;
            bool isSecond = Phase == GamePhase.DrawingSecondCard;
            if (isSecond && card == Resource.Machinery)
                return ActionResult.Fail("Открытую технику нельзя брать второй картой.");

            var player = CurrentPlayer;
            player.AddCard(card);
            _faceUp[slot] = _deck.TryDraw(out var replacement) ? replacement : (Resource?)null;
            Emit($"{player.Name} берёт открытую карту: {ResourceInfo.RussianName(card)}.");
            FixTooMuchMachinery();

            // Открытая техника первой картой заменяет обе карты.
            if (isSecond || card == Resource.Machinery || !CanDrawSecondCard()) EndTurn();
            else
            {
                Phase = GamePhase.DrawingSecondCard;
                Changed();
            }
            return ActionResult.Ok();
        }

        ActionResult DoDrawCardFromDeck()
        {
            if (Phase != GamePhase.AwaitingAction && Phase != GamePhase.DrawingSecondCard)
                return ActionResult.Fail("Сейчас нельзя брать карты.");
            if (!_deck.TryDraw(out var card)) return ActionResult.Fail("Колода пуста.");

            var player = CurrentPlayer;
            player.AddCard(card);
            Emit($"{player.Name} берёт карту из колоды.");

            if (Phase == GamePhase.DrawingSecondCard || !CanDrawSecondCard()) EndTurn();
            else
            {
                Phase = GamePhase.DrawingSecondCard;
                Changed();
            }
            return ActionResult.Ok();
        }

        /// <param name="resource">Чем платить; Machinery — только техникой.</param>
        /// <param name="machinery">Сколько карт техники добавить.</param>
        ActionResult DoBuildRoad(string roadId, Resource resource, int machinery)
        {
            if (Phase != GamePhase.AwaitingAction) return ActionResult.Fail("Сейчас нельзя строить.");

            var road = Map.GetRoad(roadId);
            if (road == null) return ActionResult.Fail("Нет такой дороги.");

            var player = CurrentPlayer;
            var unavailable = CheckRoadAvailable(player, road);
            if (unavailable != null) return ActionResult.Fail(unavailable);

            int cost = BuildCost(road);
            if (machinery < 0 || machinery > cost) return ActionResult.Fail("Неверное число карт техники.");
            int resourceCards = cost - machinery;
            if (resource == Resource.Machinery)
            {
                if (resourceCards != 0)
                    return ActionResult.Fail("Чтобы платить только техникой, её нужно столько, сколько стоит дорога.");
            }
            else if (resourceCards > 0 && road.Resource.HasValue && road.Resource.Value != resource)
            {
                return ActionResult.Fail($"Эту дорогу строят из ресурса «{ResourceInfo.RussianName(road.Resource.Value)}».");
            }

            if (player.CardCount(Resource.Machinery) < machinery) return ActionResult.Fail("Не хватает техники.");
            if (resourceCards > 0 && player.CardCount(resource) < resourceCards)
                return ActionResult.Fail($"Не хватает карт «{ResourceInfo.RussianName(resource)}».");

            Pay(player, resource, resourceCards);
            Pay(player, Resource.Machinery, machinery);
            PlaceRoad(player, road, "");
            return ActionResult.Ok();
        }

        /// <summary>Можно ли Прорабу построить эту дорогу бесплатно прямо сейчас.</summary>
        public bool CanBuildRoadFree(string roadId)
        {
            if (Phase != GamePhase.AwaitingAction) return false;
            var road = Map.GetRoad(roadId);
            var player = CurrentPlayer;
            return road != null && player.Character == CharacterKind.Foreman && player.FreeRoadsLeft > 0
                   && road.Length <= Config.ForemanMaxLength && CheckRoadAvailable(player, road) == null;
        }

        /// <summary>Прораб: построить дорогу длиной до ForemanMaxLength без карт. Очки и участки — как обычно.</summary>
        ActionResult DoBuildRoadFree(string roadId)
        {
            if (Phase != GamePhase.AwaitingAction) return ActionResult.Fail("Сейчас нельзя строить.");
            var road = Map.GetRoad(roadId);
            if (road == null) return ActionResult.Fail("Нет такой дороги.");
            var player = CurrentPlayer;
            if (player.Character != CharacterKind.Foreman) return ActionResult.Fail("Бесплатно строит только Прораб.");
            if (player.FreeRoadsLeft <= 0) return ActionResult.Fail("Бесплатные стройки закончились.");
            if (road.Length > Config.ForemanMaxLength)
                return ActionResult.Fail($"Бесплатно — только дороги длиной до {Config.ForemanMaxLength}.");
            var unavailable = CheckRoadAvailable(player, road);
            if (unavailable != null) return ActionResult.Fail(unavailable);

            player.FreeRoadsLeft--;
            PlaceRoad(player, road, " бесплатно (Прораб)");
            return ActionResult.Ok();
        }

        /// <summary>Положить дорогу игрока на карту, начислить очки, проверить цели и закончить ход.</summary>
        void PlaceRoad(PlayerState player, Road road, string note)
        {
            player.RoadPieces -= road.Length;
            player.AddRoad(road);
            _roadOwners[road.Id] = player.Index;

            int points = Config.PointsForLength(road.Length);
            if (CurrentEvent != null && CurrentEvent.Kind == CityEventKind.Investor) points += Config.InvestorBonus;
            player.RoadPoints += points;
            if (CurrentEvent != null && CurrentEvent.Kind == CityEventKind.RushHour) _rushHourRoads.Add(road.Id);
            Emit($"{player.Name} строит{note} {ResourceInfo.RoadKind(road.Resource)} {Map.LocationName(road.A)} — {Map.LocationName(road.B)} (+{points}).");
            CheckGoals(player);
            AwardInfluenceForRoad(player, road);

            EndTurn();
        }

        /// <param name="extra">Сколько контрактов сверх обычного (влияние «Контракты с выбором»).</param>
        ActionResult DoDrawContracts(int extra = 0)
        {
            if (Phase != GamePhase.AwaitingAction) return ActionResult.Fail("Сейчас нельзя брать контракты.");
            if (_contractDeck.Count == 0) return ActionResult.Fail("Контракты закончились.");

            var player = CurrentPlayer;
            player.PendingList.AddRange(_contractDeck.Draw(Config.ContractsDrawn + extra));
            Phase = GamePhase.ChoosingContracts;
            Emit($"{player.Name} берёт контракты: {player.PendingContracts.Count} шт.");
            Changed();
            return ActionResult.Ok();
        }

        ActionResult DoKeepContracts(IList<string> contractIds)
        {
            if (Phase != GamePhase.ChoosingContracts) return ActionResult.Fail("Сейчас не нужно выбирать контракты.");

            var player = CurrentPlayer;
            var check = ResolveKept(player, contractIds, Config.ContractsMinKeep, out var kept);
            if (!check.Success) return check;

            ApplyKept(player, kept);
            Emit($"{player.Name} оставляет контрактов: {kept.Count}.");
            EndTurn();
            return ActionResult.Ok();
        }

        /// <summary>Пас — только если других действий нет. Если спасовали все подряд, партия заканчивается.</summary>
        ActionResult DoPass()
        {
            if (Phase != GamePhase.AwaitingAction) return ActionResult.Fail("Сейчас нельзя пропустить ход.");
            if (HasAnyLegalAction()) return ActionResult.Fail("Пропустить ход можно, только если нет других действий.");

            Emit($"{CurrentPlayer.Name} пропускает ход.");
            _consecutivePasses++;
            if (_consecutivePasses >= _players.Count)
            {
                Emit("Ходов больше нет ни у кого.");
                FinishGame();
                return ActionResult.Ok();
            }
            EndTurn(passed: true);
            return ActionResult.Ok();
        }

        // ================================================================== Внутреннее

        static IEnumerable<Resource> BuildCardSet(GameConfig config)
        {
            for (int r = 0; r < ResourceInfo.Building.Length; r++)
            {
                int count = r < config.CardsPerResource.Length ? config.CardsPerResource[r] : 0;
                for (int i = 0; i < count; i++) yield return ResourceInfo.Building[r];
            }
            for (int i = 0; i < config.MachineryCards; i++) yield return Resource.Machinery;
        }

        string CheckRoadAvailable(PlayerState player, Road road)
        {
            if (_roadOwners.TryGetValue(road.Id, out var owner))
                return owner == player.Index ? "Эта дорога уже ваша." : $"Дорогу уже построил {_players[owner].Name}.";
            if (!IsRoadOpen(road.Id)) return "Этот район ещё не открыт.";
            if (_repairRoads.Contains(road.Id)) return "Ремонт: участок закрыт до конца круга.";
            if (IsParadeClosed(road.Id) && !(_permitActive && player.Index == CurrentPlayerIndex))
                return "Парад: дороги у этого района закрыты до конца круга (разрешение мэрии снимает запрет).";
            if (player.RoadPieces < road.Length) return "Не хватает участков дороги.";
            return null;
        }

        void Pay(PlayerState player, Resource resource, int count)
        {
            if (count <= 0) return;
            player.RemoveCards(resource, count);
            for (int i = 0; i < count; i++) _deck.Discard(resource);
        }

        bool CanDrawSecondCard() =>
            _deck.AvailableCount > 0 || _faceUp.Any(c => c.HasValue && c.Value != Resource.Machinery);

        void RefillFaceUpRow()
        {
            FillEmptySlots();
            FixTooMuchMachinery();
        }

        void FillEmptySlots()
        {
            for (int i = 0; i < _faceUp.Length; i++)
                if (!_faceUp[i].HasValue && _deck.TryDraw(out var card))
                    _faceUp[i] = card;
        }

        /// <summary>Слишком много открытой техники — ряд уходит в сброс и выкладывается заново.</summary>
        void FixTooMuchMachinery()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int machinery = _faceUp.Count(c => c == Resource.Machinery);
                if (machinery <= Config.MaxFaceUpMachinery) return;
                if (_deck.AvailableCount < _faceUp.Length) return;

                for (int i = 0; i < _faceUp.Length; i++)
                {
                    if (!_faceUp[i].HasValue) continue;
                    _deck.Discard(_faceUp[i].Value);
                    _faceUp[i] = null;
                }
                FillEmptySlots();
                Emit("Открыто слишком много техники — ряд выложен заново.");
            }
        }

        ActionResult ResolveKept(PlayerState player, IList<string> ids, int minKeep, out List<Contract> kept)
        {
            kept = new List<Contract>();
            int required = Math.Min(minKeep, player.PendingContracts.Count);

            foreach (var id in ids ?? new string[0])
            {
                Contract contract = null;
                foreach (var pending in player.PendingContracts)
                    if (pending.Id == id) contract = pending;

                if (contract == null) return ActionResult.Fail($"Контракта {id} нет среди предложенных.");
                if (kept.Contains(contract)) return ActionResult.Fail($"Контракт {id} указан дважды.");
                kept.Add(contract);
            }

            if (kept.Count < required) return ActionResult.Fail($"Нужно оставить хотя бы {required} шт.");
            return ActionResult.Ok();
        }

        void ApplyKept(PlayerState player, List<Contract> kept)
        {
            var returned = new List<Contract>();
            foreach (var contract in player.PendingContracts)
            {
                if (kept.Contains(contract)) player.ContractList.Add(contract);
                else returned.Add(contract);
            }
            player.PendingList.Clear();
            _contractDeck.ReturnToBottom(returned);
        }

        void CheckSetupComplete()
        {
            if (Phase != GamePhase.SetupContracts) return;
            if (_players.Any(p => p.PendingContracts.Count > 0)) return;

            Phase = GamePhase.AwaitingAction;
            CurrentPlayerIndex = 0;
            TurnNumber = 1;
            Emit($"Игра началась. Ходит {CurrentPlayer.Name}.");
            StartRound();
        }

        void EndTurn(bool passed = false)
        {
            if (!passed) _consecutivePasses = 0;
            var player = CurrentPlayer;

            if (_finalTurnsLeft > 0)
            {
                _finalTurnsLeft--;
                if (_finalTurnsLeft == 0)
                {
                    FinishGame();
                    return;
                }
            }
            else if (_finalTurnsLeft < 0 && player.RoadPieces <= Config.FinalRoundPieces)
            {
                // Каждый, включая этого игрока, делает ещё по одному ходу.
                _finalTurnsLeft = _players.Count;
                Emit($"У компании {player.Name} осталось участков: {player.RoadPieces}. Начинается последний круг.");
            }

            RefillFaceUpRow();
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _players.Count;
            TurnNumber++;
            Phase = GamePhase.AwaitingAction;
            _luckTakenThisTurn = false;
            _tradeUsedThisTurn = false;
            ResetInfluenceTurn();
            if (CurrentPlayerIndex == 0) StartRound(); // новый круг — новое событие
            Changed();
        }

        void FinishGame()
        {
            Phase = GamePhase.GameOver;
            Result = Scoring.Calculate(Map, _players, GetRoadOwner, TollFor, Config);
            Emit("Игра окончена. Победа: " + string.Join(", ", Result.Winners.Select(w => w.PlayerName)) + ".");
            Changed();
        }

        // ================================================================== События

        List<string> ClosedExpansions() =>
            Map.Districts.Where(d => d.IsExpansion && !_openDistricts.Contains(d.Id)).Select(d => d.Id)
                .OrderBy(id => id, StringComparer.Ordinal).ToList();

        void RebuildEventDeck()
        {
            for (int i = 0; i < Config.RushHourEvents; i++) _eventDeck.Add(CityEventKind.RushHour);
            for (int i = 0; i < Config.RepairEvents; i++) _eventDeck.Add(CityEventKind.Repair);
            for (int i = 0; i < Config.SubsidyEvents; i++) _eventDeck.Add(CityEventKind.Subsidy);
            for (int i = 0; i < Config.ParadeEvents; i++) _eventDeck.Add(CityEventKind.Parade);
            for (int i = 0; i < Config.SnowEvents; i++) _eventDeck.Add(CityEventKind.Snow);
            for (int i = 0; i < Config.InvestorEvents; i++) _eventDeck.Add(CityEventKind.Investor);
            int closedCount = ClosedExpansions().Count;
            for (int i = 0; i < closedCount; i++) _eventDeck.Add(CityEventKind.NewDistrict);
            ShuffleUtil.InPlace(_eventDeck, _rng);
        }

        /// <summary>Начало круга: прошлое событие заканчивается, открывается следующее.</summary>
        void StartRound()
        {
            AwardCatchUpInfluence();
            _repairRoads.Clear();
            CurrentEvent = null;
            if (!Config.EventsEnabled) return;
            // Не каждый круг богат на события: с шансом 1 − EventChance — спокойный день.
            if (Config.EventChance < 1f && _rng.NextDouble() >= Config.EventChance) return;

            CityEventKind? kind = null;
            for (int attempt = 0; attempt < 50 && !kind.HasValue; attempt++)
            {
                if (_eventDeck.Count == 0) RebuildEventDeck();
                if (_eventDeck.Count == 0) return;
                var next = _eventDeck[_eventDeck.Count - 1];
                _eventDeck.RemoveAt(_eventDeck.Count - 1);
                // «Новый район», когда открывать уже нечего, пропускаем.
                if (next == CityEventKind.NewDistrict && ClosedExpansions().Count == 0) continue;
                kind = next;
            }
            if (!kind.HasValue) return;
            ApplyEvent(kind.Value, null);
        }

        /// <summary>Открыть событие. target — конкретный участок или район (для тестов), иначе выбирается случайно.</summary>
        void ApplyEvent(CityEventKind kind, string target)
        {
            _repairRoads.Clear();
            CityEvent cityEvent;
            switch (kind)
            {
                case CityEventKind.Repair:
                {
                    List<string> closed;
                    if (target != null) closed = new List<string> { target };
                    else
                    {
                        var candidates = Map.Roads
                            .Where(r => !_roadOwners.ContainsKey(r.Id) && IsRoadOpen(r.Id))
                            .Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
                        ShuffleUtil.InPlace(candidates, _rng);
                        closed = candidates.Take(Config.RepairRoads).OrderBy(id => id, StringComparer.Ordinal).ToList();
                    }
                    foreach (var id in closed) _repairRoads.Add(id);
                    cityEvent = new CityEvent(kind, closed);
                    break;
                }
                case CityEventKind.NewDistrict:
                {
                    var closedDistricts = ClosedExpansions();
                    string district = target ?? (closedDistricts.Count > 0 ? closedDistricts[_rng.Next(closedDistricts.Count)] : null);
                    if (district != null)
                    {
                        _openDistricts.Add(district);
                        // Контракты нового района — наверх колоды, чтобы их можно было взять сразу.
                        var fresh = Map.Contracts
                            .Where(c => c.Districts.Contains(district) && IsContractOpen(c))
                            .ToList();
                        ShuffleUtil.InPlace(fresh, _rng);
                        _contractDeck.PutOnTop(fresh);
                    }
                    cityEvent = new CityEvent(kind, null, district);
                    break;
                }
                default:
                    cityEvent = new CityEvent(kind);
                    break;
            }

            CurrentEvent = cityEvent;
            EventNumber++;
            Emit($"Событие: {cityEvent.Title}. {cityEvent.Describe(this)}");
        }

        void Emit(string message) => Log?.Invoke(message);

        void Changed() => StateChanged?.Invoke();

        // ================================================================== Для тестов

        internal void Debug_SetHand(int playerIndex, params Resource[] cards)
        {
            var player = _players[playerIndex];
            player.ClearHand();
            foreach (var card in cards) player.AddCard(card);
        }

        internal void Debug_SetFaceUp(params Resource?[] cards)
        {
            for (int i = 0; i < _faceUp.Length; i++) _faceUp[i] = i < cards.Length ? cards[i] : null;
        }

        internal void Debug_SetPieces(int playerIndex, int pieces) => _players[playerIndex].RoadPieces = pieces;

        /// <summary>Сразу отдать дорогу игроку, без карт и хода (для тестов подсчёта).</summary>
        internal void Debug_GiveRoad(int playerIndex, string roadId)
        {
            var road = Map.GetRoad(roadId);
            _roadOwners[road.Id] = playerIndex;
            _players[playerIndex].AddRoad(road);
        }

        internal void Debug_GiveContract(int playerIndex, string contractId) =>
            _players[playerIndex].ContractList.Add(Map.GetContract(contractId));

        internal void Debug_EmptyCardSupply()
        {
            _deck.Clear();
            for (int i = 0; i < _faceUp.Length; i++) _faceUp[i] = null;
        }

        internal void Debug_Finish() => FinishGame();

        /// <summary>Открыть нужное событие прямо сейчас (target — участок для ремонта или район).</summary>
        internal void Debug_StartEvent(CityEventKind kind, string target = null) => ApplyEvent(kind, target);

        /// <summary>Сколько карт сейчас в игре (руки + открытый ряд + колода + сброс).</summary>
        internal int Debug_CountAllCards() =>
            _players.Sum(p => p.HandSize) + _faceUp.Count(c => c.HasValue) + _deck.AvailableCount;
    }
}
