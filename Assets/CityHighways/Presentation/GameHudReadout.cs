using System;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>Classic HUD display values. The screen supplies session and privacy flags; Core owns game rules.</summary>
    public static class GameHudReadout
    {
        static bool EnglishUi => LocalizationService.Language == "en";
        static string DisplayPlace(string name) => MagnateLocationText.Name(name);

        public readonly struct PlayerData
        {
            public readonly string Name, Ability, Stats;
            public readonly bool Active, HasAbility;
            public readonly int Score;
            public readonly float PiecesRatio;

            public PlayerData(string name, string ability, string stats, bool active, bool hasAbility, int score, float piecesRatio)
            {
                Name = name;
                Ability = ability;
                Stats = stats;
                Active = active;
                HasAbility = hasAbility;
                Score = score;
                PiecesRatio = piecesRatio;
            }
        }

        public static PlayerData Player(Game game, PlayerState player, string botTag)
        {
            int start = Math.Max(1, game.Config.RoadPiecesFor(game.Players.Count));
            bool hasAbility = player.Character != CharacterKind.None;
            string character = hasAbility ? " · " + Characters.Title(player.Character) : "";
            string ability = !hasAbility ? "" : CharacterLocalization.Ability(player.Character, game.Config)
                + (player.Character == CharacterKind.Foreman ? (EnglishUi ? $" Remaining: {player.FreeRoadsLeft}." : $" Осталось: {player.FreeRoadsLeft}.") : "");
            string stats = EnglishUi ? $"{player.RoadPieces} roads · {player.HandSize} cards" + (game.Config.InfluenceEnabled ? $" · {player.Influence} inf." : "") : $"{player.RoadPieces} уч · {player.HandSize} карт" + (game.Config.InfluenceEnabled ? $" · {player.Influence} вл" : "");
            return new PlayerData(player.Name + character + botTag, ability, stats,
                player.Index == game.ActingPlayerIndex, hasAbility, game.VisibleScore(player), player.RoadPieces / (float)start);
        }

        public static string Instruction(Game game, int acting, bool awaitingHost, bool realBot, bool remoteOrBot, bool needsHandoff)
        {
            if (acting < 0) return LocalizationService.Get("game.ui.092");
            if (awaitingHost) return LocalizationService.Get("game.ui.039");
            if (realBot) return LocalizationService.Get("game.ui.093");
            if (remoteOrBot) return EnglishUi ? $"{game.Players[acting].Name} is playing online…" : $"Ходит {game.Players[acting].Name} (по сети)…";
            if (needsHandoff) return LocalizationService.Get("game.ui.094");

            switch (game.Phase)
            {
                case GamePhase.SetupContracts:
                    return game.ActingPlayerIndex >= 0 && game.Players[game.ActingPlayerIndex].CharacterOffer.Count > 0
                        ? LocalizationService.Get("game.ui.095")
                        : LocalizationService.Get("game.ui.096");
                case GamePhase.AwaitingAction:
                    return LocalizationService.Get("game.ui.097");
                case GamePhase.DrawingSecondCard:
                    return LocalizationService.Get("game.ui.098");
                case GamePhase.ChoosingContracts:
                    return LocalizationService.Get("game.ui.099");
                case GamePhase.ChoosingLuck:
                    return LocalizationService.Get("game.ui.100");
                case GamePhase.RespondingTrade:
                    return LocalizationService.Get("game.ui.101");
                case GamePhase.Bidding:
                    return LocalizationService.Get("game.ui.102");
                default:
                    return "";
            }
        }

        public static string FinalText(Game game)
        {
            if (game.IsFinalRound)
            {
                int left = game.FinalTurnsLeft;
                string text = EnglishUi ? (left <= 1 ? "Final round: this is the last turn!" : $"Final round: {left} turns remaining")
                    : (left <= 1 ? "Последний круг: это последний ход партии!" : $"Последний круг: до конца {left} {Plural(left, "ход", "хода", "ходов")}");
                var order = game.FinalOrderRoad;
                if (order != null)
                    text += game.FinalOrderWinner >= 0
                        ? (EnglishUi ? $"\nFinal city order completed by {game.Players[game.FinalOrderWinner].Name}" : $"\nФинальный заказ выполнил {game.Players[game.FinalOrderWinner].Name}")
                        : (EnglishUi ? $"\nFinal city order: {DisplayPlace(game.Map.LocationName(order.A))} — {DisplayPlace(game.Map.LocationName(order.B))} (+{game.Config.FinalOrderBonus})" : $"\nФинальный заказ: {game.Map.LocationName(order.A)} — {game.Map.LocationName(order.B)} (+{game.Config.FinalOrderBonus})");
                return text;
            }
            if (game.FinalWarned)
            {
                int toFinal = game.FewestPieces - game.Config.FinalRoundPieces;
                return toFinal > 0 ? (EnglishUi ? $"Final round approaching: the building leader has {toFinal} road pieces above the final-round threshold" : $"Скоро финал: до последнего круга {toFinal} {Plural(toFinal, "участок", "участка", "участков")} у лидера стройки") : "";
            }
            return "";
        }

        static string Plural(int n, string one, string few, string many)
        {
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return one;
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return few;
            return many;
        }
    }
}
