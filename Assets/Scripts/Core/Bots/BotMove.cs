using System.Collections.Generic;

namespace RailGame.Core
{
    public enum BotMoveKind
    {
        KeepInitialTickets,
        KeepTickets,
        DrawFaceUp,
        DrawFromDeck,
        ClaimRoute,
        DrawTickets,
        Pass
    }

    /// <summary>
    /// Решение бота ещё до того, как оно выполнено. Нужно, чтобы показывать
    /// подсказку человеку («я бы на вашем месте…») тем же алгоритмом, которым играет бот.
    /// </summary>
    public sealed class BotMove
    {
        public BotMoveKind Kind { get; private set; }
        public int PlayerIndex { get; private set; } = -1;
        public int Slot { get; private set; } = -1;
        public string RouteId { get; private set; }
        public CardColor Color { get; private set; }
        public int Locomotives { get; private set; }
        public IReadOnlyList<string> TicketIds { get; private set; }

        BotMove() { }

        public static BotMove KeepInitial(int player, List<string> ids) =>
            new BotMove { Kind = BotMoveKind.KeepInitialTickets, PlayerIndex = player, TicketIds = ids };

        public static BotMove Keep(List<string> ids) => new BotMove { Kind = BotMoveKind.KeepTickets, TicketIds = ids };

        public static BotMove FaceUp(int slot) => new BotMove { Kind = BotMoveKind.DrawFaceUp, Slot = slot };

        public static BotMove Deck() => new BotMove { Kind = BotMoveKind.DrawFromDeck };

        public static BotMove Claim(string routeId, CardColor color, int locomotives) =>
            new BotMove { Kind = BotMoveKind.ClaimRoute, RouteId = routeId, Color = color, Locomotives = locomotives };

        public static BotMove Tickets() => new BotMove { Kind = BotMoveKind.DrawTickets };

        public static BotMove PassTurn() => new BotMove { Kind = BotMoveKind.Pass };

        public ActionResult Apply(Game game)
        {
            switch (Kind)
            {
                case BotMoveKind.KeepInitialTickets: return game.KeepInitialTickets(PlayerIndex, new List<string>(TicketIds));
                case BotMoveKind.KeepTickets: return game.KeepTickets(new List<string>(TicketIds));
                case BotMoveKind.DrawFaceUp: return game.DrawFaceUpCard(Slot);
                case BotMoveKind.DrawFromDeck: return game.DrawCardFromDeck();
                case BotMoveKind.ClaimRoute: return game.ClaimRoute(RouteId, Color, Locomotives);
                case BotMoveKind.DrawTickets: return game.DrawTickets();
                default: return game.Pass();
            }
        }

        /// <summary>Описание хода для подсказки игроку.</summary>
        public string Describe(Game game)
        {
            switch (Kind)
            {
                case BotMoveKind.KeepInitialTickets:
                case BotMoveKind.KeepTickets:
                    return "Оставьте задания: " + string.Join(", ", DescribeTickets(game)) + ".";
                case BotMoveKind.DrawFaceUp:
                    var card = game.FaceUpCards[Slot];
                    string name = card.HasValue ? CardColors.RussianName(card.Value) : "?";
                    return $"Возьмите открытую карту: {name}.";
                case BotMoveKind.DrawFromDeck:
                    return "Возьмите карту из колоды — нужных цветов в открытом ряду нет.";
                case BotMoveKind.ClaimRoute:
                    var route = game.Map.GetRoute(RouteId);
                    return $"Займите маршрут {CityName(game, route.CityA)} — {CityName(game, route.CityB)}.";
                case BotMoveKind.DrawTickets:
                    return "Возьмите новые задания — вагонов ещё много.";
                default:
                    return "Ходов нет — пропустите ход.";
            }
        }

        IEnumerable<string> DescribeTickets(Game game)
        {
            foreach (var id in TicketIds)
            {
                Ticket ticket = null;
                foreach (var p in game.Players)
                    foreach (var t in p.PendingTickets)
                        if (t.Id == id) ticket = t;
                yield return ticket == null ? id : $"{CityName(game, ticket.CityA)} — {CityName(game, ticket.CityB)}";
            }
        }

        static string CityName(Game game, string id) => game.Map.GetCity(id)?.Name ?? id;
    }
}
