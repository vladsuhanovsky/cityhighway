using System;
using System.Collections.Generic;
using System.Linq;

namespace RailGame.Core
{
    /// <summary>
    /// Правила игры. Ничего не знает про Unity и графику: получает команды
    /// (взять карту, занять маршрут, взять задания) и меняет состояние.
    /// Каждая команда возвращает ActionResult: при ошибке состояние не меняется.
    /// </summary>
    public sealed class Game
    {
        readonly List<PlayerState> _players = new List<PlayerState>();
        readonly CardColor?[] _faceUp;
        readonly Dictionary<string, int> _routeOwners = new Dictionary<string, int>();
        readonly CardDeck _deck;
        readonly TicketDeck _ticketDeck;

        int _finalTurnsLeft = -1; // -1 — последний круг ещё не начался
        int _consecutivePasses;

        public MapDefinition Map { get; }
        public GameConfig Config { get; }
        public IReadOnlyList<PlayerState> Players => _players;
        public int CurrentPlayerIndex { get; private set; }
        public PlayerState CurrentPlayer => _players[CurrentPlayerIndex];
        public GamePhase Phase { get; private set; }
        public int TurnNumber { get; private set; }

        /// <summary>Открытый ряд карт. null — пустая ячейка (если колода кончилась).</summary>
        public IReadOnlyList<CardColor?> FaceUpCards => _faceUp;
        public int DeckCount => _deck.DrawPileCount;
        public int DiscardCount => _deck.DiscardPileCount;
        public int TicketDeckCount => _ticketDeck.Count;
        public bool IsFinalRound => _finalTurnsLeft > 0;
        public GameResult Result { get; private set; }

        /// <summary>Текстовый журнал событий — для консоли, лога партии, подсказок в UI.</summary>
        public event Action<string> Log;
        /// <summary>Вызывается после каждого успешного действия — UI перерисовывается по нему.</summary>
        public event Action StateChanged;
        /// <summary>Структурированные события (для анимаций, звуков, сети).</summary>
        public event Action<GameEvent> EventRaised;

        public Game(MapDefinition map, IList<string> playerNames, GameConfig config = null, int? seed = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Config = config ?? new GameConfig();

            if (playerNames == null || playerNames.Count < Config.MinPlayers || playerNames.Count > Config.MaxPlayers)
                throw new ArgumentException($"Нужно от {Config.MinPlayers} до {Config.MaxPlayers} игроков.");

            var errors = map.Validate(Config.MaxRouteLength);
            if (errors.Count > 0)
                throw new ArgumentException("Карта содержит ошибки:\n" + string.Join("\n", errors));

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _deck = new CardDeck(BuildCardSet(Config), rng);
            _ticketDeck = new TicketDeck(map.Tickets, rng);
            _faceUp = new CardColor?[Config.FaceUpSlots];

            for (int i = 0; i < playerNames.Count; i++)
                _players.Add(new PlayerState(i, playerNames[i], Config.TrainsPerPlayer));

            foreach (var player in _players)
            {
                for (int k = 0; k < Config.StartingHandSize; k++)
                    if (_deck.TryDraw(out var card)) player.AddCard(card);
                player.PendingList.AddRange(_ticketDeck.Draw(Config.InitialTicketsDealt));
            }

            RefillFaceUpRow();
            Phase = GamePhase.SetupTickets;
            CheckSetupComplete();
        }

        // ------------------------------------------------------------------ Запросы

        public int? GetRouteOwner(string routeId) =>
            _routeOwners.TryGetValue(routeId, out var owner) ? owner : (int?)null;

        /// <summary>
        /// Игрок, от которого сейчас ждут действия. На старте — первый, кто ещё не выбрал задания;
        /// после конца игры — -1.
        /// </summary>
        public int ActingPlayerIndex
        {
            get
            {
                if (Phase == GamePhase.GameOver) return -1;
                if (Phase == GamePhase.SetupTickets)
                {
                    foreach (var p in _players)
                        if (p.PendingTickets.Count > 0) return p.Index;
                    return -1;
                }
                return CurrentPlayerIndex;
            }
        }

        /// <summary>
        /// Может ли игрок в принципе занять маршрут: он свободен и не закрыт правилами двойных путей.
        /// Карты и вагоны не учитываются — это для планирования, а не для проверки хода.
        /// </summary>
        public bool IsRouteOpenFor(int playerIndex, string routeId)
        {
            var route = Map.GetRoute(routeId);
            if (route == null || _routeOwners.ContainsKey(route.Id)) return false;
            if (route.ParallelRouteId != null && _routeOwners.TryGetValue(route.ParallelRouteId, out var twinOwner))
            {
                if (twinOwner == playerIndex) return false;
                if (_players.Count < Config.DoubleRoutesMinPlayers) return false;
            }
            return true;
        }

        public bool CanDrawAnyCard() => _deck.AvailableCount > 0 || _faceUp.Any(c => c.HasValue);

        /// <summary>Все способы, которыми текущий игрок может оплатить маршрут прямо сейчас.</summary>
        public IReadOnlyList<ClaimOption> GetClaimOptions(string routeId)
        {
            var options = new List<ClaimOption>();
            if (Phase != GamePhase.AwaitingAction) return options;

            var route = Map.GetRoute(routeId);
            var player = CurrentPlayer;
            if (route == null || CheckRouteAvailable(player, route) != null) return options;

            int locomotives = player.CardCount(CardColor.Locomotive);
            IEnumerable<CardColor> colors = route.Color.HasValue
                ? new[] { route.Color.Value }
                : (IEnumerable<CardColor>)CardColors.Regular;

            foreach (var color in colors)
            {
                int have = player.CardCount(color);
                if (have == 0) continue;
                int needLocomotives = route.Length - Math.Min(have, route.Length);
                if (needLocomotives <= locomotives) options.Add(new ClaimOption(color, needLocomotives));
            }

            if (locomotives >= route.Length) options.Add(new ClaimOption(CardColor.Locomotive, route.Length));
            return options;
        }

        /// <summary>Почему текущий игрок не может занять маршрут прямо сейчас; null — может.</summary>
        public string GetClaimBlocker(string routeId)
        {
            if (Phase != GamePhase.AwaitingAction) return "Сейчас нельзя занимать маршрут.";
            var route = Map.GetRoute(routeId);
            if (route == null) return "Нет такого маршрута.";
            var reason = CheckRouteAvailable(CurrentPlayer, route);
            if (reason != null) return reason;
            if (GetClaimOptions(routeId).Count == 0)
            {
                string need = route.Color.HasValue
                    ? $"{route.Length} карт(ы) цвета «{CardColors.RussianName(route.Color.Value)}»"
                    : $"{route.Length} карт(ы) одного цвета";
                return $"Нужно {need} (локомотивы подходят вместо любых).";
            }
            return null;
        }

        /// <summary>Есть ли у текущего игрока хоть одно действие, кроме пропуска хода.</summary>
        public bool HasAnyLegalAction()
        {
            if (Phase != GamePhase.AwaitingAction) return true;
            if (CanDrawAnyCard() || _ticketDeck.Count > 0) return true;
            return Map.Routes.Any(r => GetClaimOptions(r.Id).Count > 0);
        }

        // ------------------------------------------------------------------ Действия

        /// <summary>Выбор стартовых заданий. Игроки делают это в любом порядке до первого хода.</summary>
        public ActionResult KeepInitialTickets(int playerIndex, IList<string> ticketIds)
        {
            if (Phase != GamePhase.SetupTickets) return ActionResult.Fail("Стартовые задания уже выбраны.");
            if (playerIndex < 0 || playerIndex >= _players.Count) return ActionResult.Fail("Нет такого игрока.");

            var player = _players[playerIndex];
            if (player.PendingTickets.Count == 0) return ActionResult.Fail($"{player.Name} уже выбрал задания.");

            var check = ResolveKeptTickets(player, ticketIds, Config.InitialTicketsMinKeep, out var kept);
            if (!check.Success) return check;

            ApplyKeptTickets(player, kept);
            Emit($"{player.Name} оставляет стартовых заданий: {kept.Count}.");
            Raise(new GameEvent(GameEventKind.TicketsKept, player.Index, count: kept.Count));
            CheckSetupComplete();
            Changed();
            return ActionResult.Ok();
        }

        public ActionResult DrawFaceUpCard(int slot)
        {
            if (Phase != GamePhase.AwaitingAction && Phase != GamePhase.DrawingSecondCard)
                return ActionResult.Fail("Сейчас нельзя брать карты.");
            if (slot < 0 || slot >= _faceUp.Length || !_faceUp[slot].HasValue)
                return ActionResult.Fail("В этой ячейке нет карты.");

            var card = _faceUp[slot].Value;
            bool isSecondCard = Phase == GamePhase.DrawingSecondCard;
            if (isSecondCard && card == CardColor.Locomotive)
                return ActionResult.Fail("Открытый локомотив нельзя брать второй картой.");

            var player = CurrentPlayer;
            player.AddCard(card);
            _faceUp[slot] = _deck.TryDraw(out var replacement) ? replacement : (CardColor?)null;
            Emit($"{player.Name} берёт открытую карту: {CardColors.RussianName(card)}.");
            Raise(new GameEvent(GameEventKind.CardTakenFaceUp, player.Index, slot, card));
            FixTooManyLocomotives();

            // Открытый локомотив первой картой заменяет обе карты.
            if (isSecondCard || card == CardColor.Locomotive || !CanDrawSecondCard()) EndTurn();
            else
            {
                Phase = GamePhase.DrawingSecondCard;
                Changed();
            }
            return ActionResult.Ok();
        }

        public ActionResult DrawCardFromDeck()
        {
            if (Phase != GamePhase.AwaitingAction && Phase != GamePhase.DrawingSecondCard)
                return ActionResult.Fail("Сейчас нельзя брать карты.");
            if (!_deck.TryDraw(out var card))
                return ActionResult.Fail("Колода пуста.");

            var player = CurrentPlayer;
            player.AddCard(card);
            Emit($"{player.Name} берёт карту из колоды.");
            Raise(new GameEvent(GameEventKind.CardTakenFromDeck, player.Index, card: card));

            if (Phase == GamePhase.DrawingSecondCard || !CanDrawSecondCard()) EndTurn();
            else
            {
                Phase = GamePhase.DrawingSecondCard;
                Changed();
            }
            return ActionResult.Ok();
        }

        /// <param name="color">Цвет оплаты. Для оплаты только локомотивами — CardColor.Locomotive.</param>
        /// <param name="locomotives">Сколько локомотивов добавить к картам цвета.</param>
        public ActionResult ClaimRoute(string routeId, CardColor color, int locomotives)
        {
            if (Phase != GamePhase.AwaitingAction) return ActionResult.Fail("Сейчас нельзя занимать маршрут.");

            var route = Map.GetRoute(routeId);
            if (route == null) return ActionResult.Fail("Нет такого маршрута.");

            var player = CurrentPlayer;
            var unavailable = CheckRouteAvailable(player, route);
            if (unavailable != null) return ActionResult.Fail(unavailable);

            if (locomotives < 0 || locomotives > route.Length)
                return ActionResult.Fail("Неверное число локомотивов.");

            int colorCards = route.Length - locomotives;
            if (color == CardColor.Locomotive)
            {
                if (colorCards != 0)
                    return ActionResult.Fail("Чтобы платить только локомотивами, их число должно равняться длине маршрута.");
            }
            else if (colorCards > 0 && route.Color.HasValue && route.Color.Value != color)
            {
                return ActionResult.Fail($"Этот маршрут оплачивается картами цвета «{CardColors.RussianName(route.Color.Value)}».");
            }

            if (player.CardCount(CardColor.Locomotive) < locomotives)
                return ActionResult.Fail("Не хватает локомотивов.");
            if (colorCards > 0 && player.CardCount(color) < colorCards)
                return ActionResult.Fail($"Не хватает карт цвета «{CardColors.RussianName(color)}».");

            // Проверки пройдены — платим.
            PayCards(player, color, colorCards);
            PayCards(player, CardColor.Locomotive, locomotives);
            player.TrainsLeft -= route.Length;
            player.AddRoute(route);
            _routeOwners[route.Id] = player.Index;

            int points = Config.PointsForRouteLength(route.Length);
            player.RoutePoints += points;
            Emit($"{player.Name} занимает маршрут {CityName(route.CityA)} — {CityName(route.CityB)} (+{points}).");
            Raise(new GameEvent(GameEventKind.RouteClaimed, player.Index, routeId: route.Id, count: points));

            EndTurn();
            return ActionResult.Ok();
        }

        public ActionResult DrawTickets()
        {
            if (Phase != GamePhase.AwaitingAction) return ActionResult.Fail("Сейчас нельзя брать задания.");
            if (_ticketDeck.Count == 0) return ActionResult.Fail("Колода заданий пуста.");

            var player = CurrentPlayer;
            player.PendingList.AddRange(_ticketDeck.Draw(Config.TicketsDrawn));
            Phase = GamePhase.ChoosingTickets;
            Emit($"{player.Name} тянет задания: {player.PendingTickets.Count} шт.");
            Raise(new GameEvent(GameEventKind.TicketsDrawn, player.Index, count: player.PendingTickets.Count));
            Changed();
            return ActionResult.Ok();
        }

        public ActionResult KeepTickets(IList<string> ticketIds)
        {
            if (Phase != GamePhase.ChoosingTickets) return ActionResult.Fail("Сейчас не нужно выбирать задания.");

            var player = CurrentPlayer;
            var check = ResolveKeptTickets(player, ticketIds, Config.TicketsMinKeep, out var kept);
            if (!check.Success) return check;

            ApplyKeptTickets(player, kept);
            Emit($"{player.Name} оставляет заданий: {kept.Count}.");
            Raise(new GameEvent(GameEventKind.TicketsKept, player.Index, count: kept.Count));
            EndTurn();
            return ActionResult.Ok();
        }

        /// <summary>Пропуск хода — только если других действий нет. Если пропустили все подряд, игра заканчивается.</summary>
        public ActionResult Pass()
        {
            if (Phase != GamePhase.AwaitingAction) return ActionResult.Fail("Сейчас нельзя пропустить ход.");
            if (HasAnyLegalAction()) return ActionResult.Fail("Пропустить ход можно, только если нет других действий.");

            Emit($"{CurrentPlayer.Name} пропускает ход.");
            Raise(new GameEvent(GameEventKind.Passed, CurrentPlayerIndex));
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

        // ------------------------------------------------------------------ Внутреннее

        static IEnumerable<CardColor> BuildCardSet(GameConfig config)
        {
            foreach (var color in CardColors.Regular)
                for (int i = 0; i < config.CardsPerColor; i++) yield return color;
            for (int i = 0; i < config.LocomotiveCards; i++) yield return CardColor.Locomotive;
        }

        string CheckRouteAvailable(PlayerState player, Route route)
        {
            if (_routeOwners.ContainsKey(route.Id)) return "Маршрут уже занят.";

            if (route.ParallelRouteId != null && _routeOwners.TryGetValue(route.ParallelRouteId, out var twinOwner))
            {
                if (twinOwner == player.Index) return "Нельзя занять оба параллельных пути.";
                if (_players.Count < Config.DoubleRoutesMinPlayers)
                    return $"Когда игроков меньше {Config.DoubleRoutesMinPlayers}, второй параллельный путь закрыт.";
            }

            if (player.TrainsLeft < route.Length) return "Не хватает вагонов.";
            return null;
        }

        void PayCards(PlayerState player, CardColor color, int count)
        {
            if (count <= 0) return;
            player.RemoveCards(color, count);
            for (int i = 0; i < count; i++) _deck.Discard(color);
        }

        bool CanDrawSecondCard() =>
            _deck.AvailableCount > 0 || _faceUp.Any(c => c.HasValue && c.Value != CardColor.Locomotive);

        void RefillFaceUpRow()
        {
            FillEmptySlots();
            FixTooManyLocomotives();
        }

        void FillEmptySlots()
        {
            for (int i = 0; i < _faceUp.Length; i++)
                if (!_faceUp[i].HasValue && _deck.TryDraw(out var card))
                    _faceUp[i] = card;
        }

        /// <summary>Слишком много открытых локомотивов — ряд уходит в сброс и выкладывается заново.</summary>
        void FixTooManyLocomotives()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int locomotives = _faceUp.Count(c => c == CardColor.Locomotive);
                if (locomotives <= Config.MaxFaceUpLocomotives) return;
                // Если в колоде и сбросе почти ничего нет, пересдача ничего не изменит.
                if (_deck.AvailableCount < _faceUp.Length) return;

                for (int i = 0; i < _faceUp.Length; i++)
                {
                    if (!_faceUp[i].HasValue) continue;
                    _deck.Discard(_faceUp[i].Value);
                    _faceUp[i] = null;
                }
                FillEmptySlots();
                Emit("Открыто слишком много локомотивов — ряд выложен заново.");
                Raise(new GameEvent(GameEventKind.FaceUpRowReset));
            }
        }

        ActionResult ResolveKeptTickets(PlayerState player, IList<string> ticketIds, int minKeep, out List<Ticket> kept)
        {
            kept = new List<Ticket>();
            var ids = ticketIds ?? new string[0];
            int required = Math.Min(minKeep, player.PendingTickets.Count);

            foreach (var id in ids)
            {
                Ticket ticket = null;
                foreach (var pending in player.PendingTickets)
                    if (pending.Id == id) ticket = pending;

                if (ticket == null) return ActionResult.Fail($"Задания {id} нет среди предложенных.");
                if (kept.Contains(ticket)) return ActionResult.Fail($"Задание {id} указано дважды.");
                kept.Add(ticket);
            }

            if (kept.Count < required) return ActionResult.Fail($"Нужно оставить хотя бы {required} шт.");
            return ActionResult.Ok();
        }

        void ApplyKeptTickets(PlayerState player, List<Ticket> kept)
        {
            var returned = new List<Ticket>();
            foreach (var ticket in player.PendingTickets)
            {
                if (kept.Contains(ticket)) player.TicketList.Add(ticket);
                else returned.Add(ticket);
            }
            player.PendingList.Clear();
            _ticketDeck.ReturnToBottom(returned);
        }

        void CheckSetupComplete()
        {
            if (Phase != GamePhase.SetupTickets) return;
            if (_players.Any(p => p.PendingTickets.Count > 0)) return;

            Phase = GamePhase.AwaitingAction;
            CurrentPlayerIndex = 0;
            TurnNumber = 1;
            Emit($"Игра началась. Ходит {CurrentPlayer.Name}.");
            Raise(new GameEvent(GameEventKind.GameStarted));
            Raise(new GameEvent(GameEventKind.TurnStarted, CurrentPlayerIndex));
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
            else if (_finalTurnsLeft < 0 && player.TrainsLeft <= Config.FinalRoundTrainThreshold)
            {
                // Каждый игрок, включая этого, делает ещё по одному ходу.
                _finalTurnsLeft = _players.Count;
                Emit($"У игрока {player.Name} осталось вагонов: {player.TrainsLeft}. Начинается последний круг.");
                Raise(new GameEvent(GameEventKind.FinalRoundStarted, player.Index));
            }

            RefillFaceUpRow();
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _players.Count;
            TurnNumber++;
            Phase = GamePhase.AwaitingAction;
            Raise(new GameEvent(GameEventKind.TurnStarted, CurrentPlayerIndex));
            Changed();
        }

        void FinishGame()
        {
            Phase = GamePhase.GameOver;
            Result = Scoring.Calculate(_players, Config);
            Emit("Игра окончена. Победа: " + string.Join(", ", Result.Winners.Select(w => w.PlayerName)) + ".");
            Raise(new GameEvent(GameEventKind.GameOver));
            Changed();
        }

        string CityName(string cityId) => Map.GetCity(cityId)?.Name ?? cityId;

        void Emit(string message) => Log?.Invoke(message);

        void Raise(GameEvent gameEvent) => EventRaised?.Invoke(gameEvent);

        void Changed() => StateChanged?.Invoke();

        // ------------------------------------------------------------------ Для тестов

        internal void Debug_SetHand(int playerIndex, params CardColor[] cards)
        {
            var player = _players[playerIndex];
            player.ClearHand();
            foreach (var card in cards) player.AddCard(card);
        }

        internal void Debug_SetFaceUp(params CardColor?[] cards)
        {
            for (int i = 0; i < _faceUp.Length; i++)
                _faceUp[i] = i < cards.Length ? cards[i] : null;
        }

        internal void Debug_SetTrains(int playerIndex, int trains) => _players[playerIndex].TrainsLeft = trains;

        /// <summary>Убирает из игры все карты колоды, сброса и открытого ряда (только для тестов).</summary>
        internal void Debug_EmptyCardSupply()
        {
            while (_deck.TryDraw(out _)) { }
            for (int i = 0; i < _faceUp.Length; i++) _faceUp[i] = null;
        }

        /// <summary>Сколько карт вагонов сейчас в игре (руки + открытый ряд + колода + сброс).</summary>
        internal int Debug_CountAllCards() =>
            _players.Sum(p => p.HandSize) + _faceUp.Count(c => c.HasValue) + _deck.AvailableCount;
    }
}
