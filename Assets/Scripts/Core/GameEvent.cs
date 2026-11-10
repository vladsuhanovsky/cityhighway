namespace RailGame.Core
{
    public enum GameEventKind
    {
        GameStarted,
        TurnStarted,
        CardTakenFaceUp,
        CardTakenFromDeck,
        FaceUpRowReset,
        RouteClaimed,
        TicketsDrawn,
        TicketsKept,
        FinalRoundStarted,
        Passed,
        GameOver
    }

    /// <summary>
    /// Структурированное событие партии. Интерфейс по ним запускает анимации и звуки,
    /// сеть сможет пересылать их клиентам.
    /// </summary>
    public sealed class GameEvent
    {
        public GameEventKind Kind { get; }
        public int Player { get; }
        /// <summary>Ячейка открытого ряда (для CardTakenFaceUp), иначе -1.</summary>
        public int Slot { get; }
        /// <summary>Взятая карта (для CardTakenFaceUp и CardTakenFromDeck).</summary>
        public CardColor Card { get; }
        public string RouteId { get; }
        /// <summary>Очки за маршрут или число заданий.</summary>
        public int Count { get; }

        public GameEvent(GameEventKind kind, int player = -1, int slot = -1, CardColor card = CardColor.Red,
            string routeId = null, int count = 0)
        {
            Kind = kind;
            Player = player;
            Slot = slot;
            Card = card;
            RouteId = routeId;
            Count = count;
        }

        public override string ToString() => $"{Kind} p={Player} slot={Slot} card={Card} route={RouteId} n={Count}";
    }
}
