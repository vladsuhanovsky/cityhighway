using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>
    /// Действие игрока в виде данных: что сделать и с какими параметрами. Плоский класс, чтобы его
    /// сериализовал JsonUtility (сохранения) и потом — сетевой код (клиент отправляет команду хосту).
    /// </summary>
    [Serializable]
    public class GameCommand
    {
        public const string KeepInitial = "keep_initial";
        public const string DrawFaceUp = "draw_face_up";
        public const string DrawDeck = "draw_deck";
        public const string Build = "build";
        public const string BuildFree = "build_free";
        public const string DrawContractsKind = "draw_contracts";
        public const string KeepContractsKind = "keep_contracts";
        public const string PassKind = "pass";
        public const string DrawLuck = "draw_luck";
        public const string ChooseLuck = "choose_luck";
        public const string Upgrade = "upgrade";
        public const string ProposeTradeKind = "propose_trade";
        public const string RespondTradeKind = "respond_trade";
        public const string InfluenceKind = "influence";
        public const string BidKind = "bid";
        public const string BribeKindName = "bribe";
        public const string AccuseKind = "accuse";

        public string kind;
        /// <summary>Кто действует. Для всех команд, кроме keep_initial, — игрок, чей сейчас ход.</summary>
        public int player = -1;
        public int slot;
        public string road;
        /// <summary>Resource как int.</summary>
        public int resource;
        public int machinery;
        public string[] contracts;
        public int index;
        // Сделка: кому, что отдаём, что просим; ответ.
        public int to;
        public int giveResource;
        public int giveCount;
        public int getResource;
        public int getCount;
        public bool accept;
        public int giveInfluence;
        public int getInfluence;
        /// <summary>Трата влияния: InfluenceAction как int.</summary>
        public int option;
        /// <summary>Ставка на аукционе мэрии.</summary>
        public int bid;

        public static GameCommand KeepInitialContracts(int player, IEnumerable<string> ids) =>
            new GameCommand { kind = KeepInitial, player = player, contracts = ids.ToArray() };
        public static GameCommand DrawFaceUpCard(int player, int slot) => new GameCommand { kind = DrawFaceUp, player = player, slot = slot };
        public static GameCommand DrawCardFromDeck(int player) => new GameCommand { kind = DrawDeck, player = player };
        public static GameCommand BuildRoad(int player, string road, Resource resource, int machinery) =>
            new GameCommand { kind = Build, player = player, road = road, resource = (int)resource, machinery = machinery };
        public static GameCommand BuildRoadFree(int player, string road) => new GameCommand { kind = BuildFree, player = player, road = road };
        public static GameCommand DrawContracts(int player) => new GameCommand { kind = DrawContractsKind, player = player };
        public static GameCommand KeepContracts(int player, IEnumerable<string> ids) =>
            new GameCommand { kind = KeepContractsKind, player = player, contracts = ids.ToArray() };
        public static GameCommand Pass(int player) => new GameCommand { kind = PassKind, player = player };
        public static GameCommand DrawLuckCard(int player) => new GameCommand { kind = DrawLuck, player = player };
        public static GameCommand ChooseLuckCard(int player, int index) => new GameCommand { kind = ChooseLuck, player = player, index = index };
        public static GameCommand UpgradeRoad(int player, string road, Resource resource, int machinery) =>
            new GameCommand { kind = Upgrade, player = player, road = road, resource = (int)resource, machinery = machinery };
        public static GameCommand ProposeTrade(int player, int to, Resource give, int giveCount, Resource get, int getCount,
            int giveInfluence = 0, int getInfluence = 0) =>
            new GameCommand { kind = ProposeTradeKind, player = player, to = to, giveResource = (int)give, giveCount = giveCount, getResource = (int)get, getCount = getCount,
                giveInfluence = giveInfluence, getInfluence = getInfluence };
        public static GameCommand UseInfluence(int player, InfluenceAction action) => new GameCommand { kind = InfluenceKind, player = player, option = (int)action };
        public static GameCommand Bid(int player, int amount) => new GameCommand { kind = BidKind, player = player, bid = amount };
        public static GameCommand Bribe(int player, BribeKind bribe) => new GameCommand { kind = BribeKindName, player = player, option = (int)bribe };
        public static GameCommand Accuse(int player, int accused) => new GameCommand { kind = AccuseKind, player = player, to = accused };
        public static GameCommand RespondTrade(int player, bool accept) => new GameCommand { kind = RespondTradeKind, player = player, accept = accept };

        public override string ToString()
        {
            switch (kind)
            {
                case KeepInitial: return $"#{player} оставляет стартовые {string.Join(",", contracts ?? new string[0])}";
                case DrawFaceUp: return $"#{player} берёт открытую карту {slot}";
                case DrawDeck: return $"#{player} берёт карту из колоды";
                case Build: return $"#{player} строит {road} за {(Resource)resource} + {machinery} техн.";
                case BuildFree: return $"#{player} строит {road} бесплатно";
                case DrawContractsKind: return $"#{player} тянет контракты";
                case KeepContractsKind: return $"#{player} оставляет {string.Join(",", contracts ?? new string[0])}";
                case PassKind: return $"#{player} пасует";
                case DrawLuck: return $"#{player} берёт карту удачи";
                case ChooseLuck: return $"#{player} оставляет карту удачи {index}";
                case Upgrade: return $"#{player} улучшает {road} за {(Resource)resource} + {machinery} техн.";
                case ProposeTradeKind: return $"#{player} предлагает #{to}: {giveCount}×{(Resource)giveResource}+{giveInfluence}вл. за {getCount}×{(Resource)getResource}+{getInfluence}вл.";
                case InfluenceKind: return $"#{player} тратит влияние: {(InfluenceAction)option}";
                case BidKind: return $"#{player} ставит {bid} на аукционе";
                case BribeKindName: return $"#{player} даёт взятку: {(BribeKind)option}";
                case AccuseKind: return $"#{player} обвиняет #{to}";
                case RespondTradeKind: return $"#{player} {(accept ? "принимает" : "отклоняет")} сделку";
                default: return $"#{player} {kind}";
            }
        }
    }

    /// <summary>С чего началась партия: по этому и списку команд её можно воспроизвести точь-в-точь.</summary>
    [Serializable]
    public class GameStartInfo
    {
        public string[] players;
        /// <summary>CharacterKind как int.</summary>
        public int[] characters;
        public int seed;
        public GameConfig config;
    }

    public sealed partial class Game
    {
        readonly List<GameCommand> _history = new List<GameCommand>();

        /// <summary>Зерно генератора партии.</summary>
        public int Seed { get; }

        /// <summary>Начальные условия; null — партия загружена из старого сохранения без истории.</summary>
        public GameStartInfo StartInfo { get; private set; }

        /// <summary>Все успешные действия игроков по порядку (Debug_-методы тестов сюда не попадают).</summary>
        public IReadOnlyList<GameCommand> History => _history;

        /// <summary>Можно ли воспроизвести партию с начала (есть начальные условия и полная история).</summary>
        public bool CanReplay => StartInfo != null;

        /// <summary>
        /// Выполнить команду. Проверяет, что действует нужный игрок, и вызывает соответствующее действие.
        /// Так хост сетевой игры будет применять команды клиентов.
        /// </summary>
        public ActionResult Execute(GameCommand command)
        {
            if (command == null) return ActionResult.Fail("Пустая команда.");
            if (command.kind != GameCommand.KeepInitial && command.player >= 0 && command.player != ActingPlayerIndex)
                return ActionResult.Fail($"Сейчас ходит не игрок {command.player}.");
            switch (command.kind)
            {
                case GameCommand.KeepInitial: return KeepInitialContracts(command.player, command.contracts ?? new string[0]);
                case GameCommand.DrawFaceUp: return DrawFaceUpCard(command.slot);
                case GameCommand.DrawDeck: return DrawCardFromDeck();
                case GameCommand.Build:
                    if (command.resource < 0 || command.resource >= ResourceInfo.Count) return ActionResult.Fail("Неизвестный ресурс.");
                    return BuildRoad(command.road, (Resource)command.resource, command.machinery);
                case GameCommand.BuildFree: return BuildRoadFree(command.road);
                case GameCommand.DrawContractsKind: return DrawContracts();
                case GameCommand.KeepContractsKind: return KeepContracts(command.contracts ?? new string[0]);
                case GameCommand.PassKind: return Pass();
                case GameCommand.DrawLuck: return DrawLuckCard();
                case GameCommand.ChooseLuck: return ChooseLuckCard(command.index);
                case GameCommand.Upgrade:
                    if (command.resource < 0 || command.resource >= ResourceInfo.Count) return ActionResult.Fail("Неизвестный ресурс.");
                    return UpgradeRoad(command.road, (Resource)command.resource, command.machinery);
                case GameCommand.ProposeTradeKind:
                    if (command.giveResource < 0 || command.giveResource >= ResourceInfo.Count
                        || command.getResource < 0 || command.getResource >= ResourceInfo.Count) return ActionResult.Fail("Неизвестный ресурс.");
                    return ProposeTrade(command.to, (Resource)command.giveResource, command.giveCount, (Resource)command.getResource, command.getCount,
                        command.giveInfluence, command.getInfluence);
                case GameCommand.InfluenceKind:
                    if (command.option < 0 || command.option > (int)InfluenceAction.CancelEvent) return ActionResult.Fail("Неизвестная трата влияния.");
                    return UseInfluence((InfluenceAction)command.option);
                case GameCommand.BidKind: return Bid(command.bid);
                case GameCommand.BribeKindName:
                    if (command.option < 0 || command.option > (int)BribeKind.ExtraCard) return ActionResult.Fail("Неизвестная взятка.");
                    return Bribe((BribeKind)command.option);
                case GameCommand.AccuseKind: return Accuse(command.to);
                case GameCommand.RespondTradeKind: return RespondTrade(command.accept);
                default: return ActionResult.Fail($"Неизвестная команда «{command.kind}».");
            }
        }

        /// <summary>Воспроизвести партию: те же начальные условия и команды дают то же состояние.</summary>
        public static Game Replay(CityMap map, GameStartInfo start, IEnumerable<GameCommand> commands)
        {
            if (start == null) throw new ArgumentNullException(nameof(start));
            var characters = (start.characters ?? new int[start.players.Length]).Select(c => (CharacterKind)c).ToList();
            var game = new Game(map, start.players, start.config, start.seed, characters);
            int number = 0;
            foreach (var command in commands ?? Enumerable.Empty<GameCommand>())
            {
                number++;
                var result = game.Execute(command);
                if (!result.Success)
                    throw new InvalidOperationException($"Команда {number} ({command}) не выполнилась: {result.Error}");
            }
            return game;
        }

        void Record(GameCommand command) => _history.Add(command);

        // ================================================================== Действия: выполнить и записать

        public ActionResult KeepInitialContracts(int playerIndex, IList<string> contractIds)
        {
            var ids = contractIds?.ToArray() ?? new string[0];
            var result = DoKeepInitialContracts(playerIndex, ids);
            if (result.Success) Record(GameCommand.KeepInitialContracts(playerIndex, ids));
            return result;
        }

        public ActionResult DrawFaceUpCard(int slot) => Recorded(CurrentPlayerIndex, DoDrawFaceUpCard(slot), p => GameCommand.DrawFaceUpCard(p, slot));
        public ActionResult DrawCardFromDeck() => Recorded(CurrentPlayerIndex, DoDrawCardFromDeck(), GameCommand.DrawCardFromDeck);
        public ActionResult BuildRoad(string roadId, Resource resource, int machinery) =>
            Recorded(CurrentPlayerIndex, DoBuildRoad(roadId, resource, machinery), p => GameCommand.BuildRoad(p, roadId, resource, machinery));
        public ActionResult BuildRoadFree(string roadId) => Recorded(CurrentPlayerIndex, DoBuildRoadFree(roadId), p => GameCommand.BuildRoadFree(p, roadId));
        public ActionResult DrawContracts() => Recorded(CurrentPlayerIndex, DoDrawContracts(), GameCommand.DrawContracts);
        public ActionResult Pass() => Recorded(CurrentPlayerIndex, DoPass(), GameCommand.Pass);
        public ActionResult DrawLuckCard() => Recorded(CurrentPlayerIndex, DoDrawLuckCard(), GameCommand.DrawLuckCard);
        public ActionResult ChooseLuckCard(int index) => Recorded(CurrentPlayerIndex, DoChooseLuckCard(index), p => GameCommand.ChooseLuckCard(p, index));
        public ActionResult UpgradeRoad(string roadId, Resource resource, int machinery) =>
            Recorded(CurrentPlayerIndex, DoUpgradeRoad(roadId, resource, machinery), p => GameCommand.UpgradeRoad(p, roadId, resource, machinery));
        public ActionResult ProposeTrade(int to, Resource give, int giveCount, Resource get, int getCount, int giveInfluence = 0, int getInfluence = 0) =>
            Recorded(CurrentPlayerIndex, DoProposeTrade(to, give, giveCount, get, getCount, giveInfluence, getInfluence),
                p => GameCommand.ProposeTrade(p, to, give, giveCount, get, getCount, giveInfluence, getInfluence));
        /// <summary>Ставку делает тот, чья очередь на аукционе (ActingPlayerIndex).</summary>
        public ActionResult Bid(int amount) => Recorded(ActingPlayerIndex, DoBid(amount), p => GameCommand.Bid(p, amount));
        public ActionResult Bribe(BribeKind kind) => Recorded(CurrentPlayerIndex, DoBribe(kind), p => GameCommand.Bribe(p, kind));
        public ActionResult Accuse(int accused) => Recorded(CurrentPlayerIndex, DoAccuse(accused), p => GameCommand.Accuse(p, accused));
        public ActionResult UseInfluence(InfluenceAction action) =>
            Recorded(CurrentPlayerIndex, DoUseInfluence(action), p => GameCommand.UseInfluence(p, action));
        /// <summary>Ответ на сделку даёт её получатель (ActingPlayerIndex), а не текущий игрок.</summary>
        public ActionResult RespondTrade(bool accept) =>
            Recorded(ActingPlayerIndex, DoRespondTrade(accept), p => GameCommand.RespondTrade(p, accept));

        public ActionResult KeepContracts(IList<string> contractIds)
        {
            var ids = contractIds?.ToArray() ?? new string[0];
            int player = CurrentPlayerIndex;
            var result = DoKeepContracts(ids);
            if (result.Success) Record(GameCommand.KeepContracts(player, ids));
            return result;
        }

        /// <summary>
        /// Обёртка для действий текущего игрока. Игрок запоминается заранее: после действия ход может
        /// перейти к следующему, а в истории должен остаться тот, кто действовал.
        /// </summary>
        /// Аргументы C# вычисляются слева направо, поэтому CurrentPlayerIndex читается до действия.
        ActionResult Recorded(int player, ActionResult result, Func<int, GameCommand> make)
        {
            if (result.Success) Record(make(player));
            return result;
        }
    }
}
