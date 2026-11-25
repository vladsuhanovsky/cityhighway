using System;
using System.Collections.Generic;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    public static class ReplayText
    {
        static bool En => LocalizationService.Language == "en";
        static string T(string en, string ru) => En ? en : ru;
        static string Place(string value) => MagnateLocationText.Name(value);

        public static string Title(ReplayFile file) =>
            (file.source == "local" ? T("Match replay", "Повтор партии") : T("Replay: ", "Повтор: ") + file.author)
            + " · " + Place(file.map);

        public static string Facts(ReplayFile file, string date)
        {
            var facts = new List<string> { file.IsCoop ? T("Co-op", "Кооператив") : T("Competitive", "Соревнование"), date };
            if (!string.IsNullOrEmpty(file.scenario) && file.scenario != Scenarios.Title(ScenarioKind.Standard))
            {
                string scenario = file.scenario;
                foreach (ScenarioKind kind in Enum.GetValues(typeof(ScenarioKind)))
                    if (scenario == Scenarios.Title(kind)) { scenario = ScenarioMenuLocalization.Title(kind); break; }
                facts.Add(scenario);
            }
            if (file.authorSeat >= 0) facts.Add(file.author + ": " + Points(file.authorScore) + (file.won ? T(", victory", ", победа") : ""));
            if (file.online) facts.Add(T("online", "сетевая"));
            return string.Join(" · ", facts);
        }

        public static string Points(int score) => score + T(" pts", " очк.");
        public static string PlayerDetails(int cards, int pieces, int roads) =>
            T($"Cards: {cards} · pieces: {pieces} · roads: {roads}", $"Карт: {cards} · участков: {pieces} · дорог: {roads}");
        public static string Coop(int done, int target, int crisis, int max) =>
            T($"Orders: {done} of {target} · crisis {crisis} of {max}", $"Заказы: {done} из {target} · кризис {crisis} из {max}");

        public static string Turn(Game game)
        {
            if (game.Phase == GamePhase.GameOver) return T("Game over", "Партия окончена");
            if (game.Phase == GamePhase.SetupContracts) return T("Setup: companies and starting contracts", "Подготовка: компании и стартовые контракты");
            int players = game.Players.Count;
            int round = players > 0 ? (game.TurnNumber - 1) / players + 1 : 1;
            return T($"Round {round} · {game.CurrentPlayer.Name} to act", $"Круг {round} · ходит {game.CurrentPlayer.Name}");
        }

        public static string Error(string key, string detail, int action)
        {
            if (key == "map") return T("Replay map not found: ", "Карта повтора не найдена: ") + detail;
            if (key == "open") return T("Could not open replay: ", "Повтор не открылся: ") + detail;
            if (key == "command") return T(
                $"Replay stopped at action {action}: “{LocalizationService.TranslateGameError(detail)}”. The match may use rules from another game version.",
                $"Повтор остановился на действии {action}: «{detail}». Скорее всего, партия сыграна по правилам другой версии игры.");
            return detail ?? "";
        }

        // Capture values before Execute. Deferred formatting never consults a later face-up row.
        public static Func<string> Describe(Game game, GameCommand command)
        {
            string Name(int index) => index >= 0 && index < game.Players.Count ? game.Players[index].Name : null;
            string who = Name(command.player);
            string Who() => who ?? T("Player", "Игрок");
            string ResourceName(int value) => value >= 0 && value < ResourceInfo.Count
                ? Palette.CardTitle((Resource)value).ToLowerInvariant() : T("cards", "карты");
            string to = Name(command.to);
            var road = string.IsNullOrEmpty(command.road) ? null : game.Map.GetRoad(command.road);
            string a = road == null ? null : game.Map.LocationName(road.A);
            string b = road == null ? null : game.Map.LocationName(road.B);
            int length = road == null ? 0 : road.Length;
            string RoadText() => road == null ? command.road : T($"{Place(a)} — {Place(b)} ({length} pcs)", $"{a} — {b} ({length} уч.)");
            switch (command.kind)
            {
                case GameCommand.KeepInitial: return () => T($"{Who()} keeps starting contracts ({command.contracts?.Length ?? 0}).", $"{Who()} оставляет стартовые контракты ({command.contracts?.Length ?? 0}).");
                case GameCommand.ChooseCharacterKind: return () => T($"{Who()} chooses company “{Characters.Title((CharacterKind)command.option)}”.", $"{Who()} выбирает компанию «{Characters.Title((CharacterKind)command.option)}».");
                case GameCommand.DrawDeck: return () => T($"{Who()} draws a card from the deck.", $"{Who()} берёт карту из колоды.");
                case GameCommand.DrawFaceUp:
                    var cards = game.FaceUpCards;
                    var card = command.slot >= 0 && command.slot < cards.Count ? cards[command.slot] : null;
                    return () => card.HasValue
                        ? T($"{Who()} draws a face-up card: {Palette.CardTitle(card.Value)}.", $"{Who()} берёт открытую карту: {ResourceInfo.RussianName(card.Value)}.")
                        : T($"{Who()} draws a face-up card.", $"{Who()} берёт открытую карту.");
                case GameCommand.Build: return () => T($"{Who()} builds road {RoadText()} with {ResourceName(command.resource)}" + (command.machinery > 0 ? $" and machinery ×{command.machinery}." : "."), $"{Who()} строит дорогу {RoadText()} за {ResourceName(command.resource)}" + (command.machinery > 0 ? $" и технику ×{command.machinery}." : "."));
                case GameCommand.BuildFree: return () => T($"{Who()} builds road {RoadText()} for free.", $"{Who()} строит дорогу {RoadText()} бесплатно.");
                case GameCommand.Upgrade: return () => T($"{Who()} upgrades {RoadText()} to a highway.", $"{Who()} улучшает дорогу {RoadText()} до магистрали.");
                case GameCommand.DrawContractsKind: return () => T($"{Who()} draws new contracts.", $"{Who()} берёт новые контракты.");
                case GameCommand.KeepContractsKind: return () => T($"{Who()} keeps contracts: {command.contracts?.Length ?? 0}.", $"{Who()} оставляет контракты: {command.contracts?.Length ?? 0}.");
                case GameCommand.PassKind: return () => T($"{Who()} skips a turn.", $"{Who()} пропускает ход.");
                case GameCommand.DrawLuck: return () => T($"{Who()} draws a luck card.", $"{Who()} тянет карту удачи.");
                case GameCommand.ChooseLuck: return () => T($"{Who()} chooses a luck card.", $"{Who()} выбирает карту удачи.");
                case GameCommand.ProposeTradeKind: return () => T($"{Who()} offers {to ?? "Player"}: {command.giveCount} × {ResourceName(command.giveResource)} for {command.getCount} × {ResourceName(command.getResource)}.", $"{Who()} предлагает сделку игроку {to ?? "Игрок"}: {command.giveCount} × {ResourceName(command.giveResource)} за {command.getCount} × {ResourceName(command.getResource)}.");
                case GameCommand.RespondTradeKind: return () => command.accept ? T($"{Who()} accepts the trade.", $"{Who()} принимает сделку.") : T($"{Who()} declines the trade.", $"{Who()} отказывается от сделки.");
                case GameCommand.InfluenceKind: return () => T($"{Who()} spends influence.", $"{Who()} тратит влияние.");
                case GameCommand.BidKind: return () => command.bid > 0 ? T($"{Who()} bids {command.bid} at City Hall's auction.", $"{Who()} ставит {command.bid} на аукционе мэрии.") : T($"{Who()} passes at City Hall's auction.", $"{Who()} пасует на аукционе мэрии.");
                case GameCommand.BribeKindName: return () => T($"{Who()} secretly negotiates with an official.", $"{Who()} тайком договаривается с чиновником.");
                case GameCommand.AccuseKind: return () => T($"{Who()} accuses {to ?? "Player"} of bribery.", $"{Who()} обвиняет {to ?? "Игрок"} во взятке.");
                default: return null;
            }
        }
    }
}
