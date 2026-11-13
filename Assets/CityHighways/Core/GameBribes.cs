using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Тайная взятка чиновникам мэрии.</summary>
    public enum BribeKind
    {
        /// <summary>«Ускорить согласование»: в этот ход дороги стоят на 1 карту меньше (не меньше 1).</summary>
        Discount,
        /// <summary>«Внеочередная поставка»: сразу 1 карта из колоды в руку, ход продолжается.</summary>
        ExtraCard
    }

    public static class BribeInfo
    {
        public static readonly BribeKind[] All = { BribeKind.Discount, BribeKind.ExtraCard };

        public static int Cost(BribeKind kind, GameConfig config) =>
            kind == BribeKind.Discount ? config.BribeDiscountCost : config.BribeExtraCardCost;

        public static string Title(BribeKind kind) =>
            kind == BribeKind.Discount ? "Ускорить согласование" : "Внеочередная поставка";

        public static string Describe(BribeKind kind) =>
            kind == BribeKind.Discount
                ? "В этот ход дороги стоят вам на 1 карту меньше (но хотя бы 1)."
                : "Сразу 1 карта из колоды в руку; ход продолжается как обычно.";
    }

    /// <summary>Итог обвинения: кто кого обвинил и подтвердилось ли.</summary>
    public sealed class AccusationResult
    {
        public int Accuser { get; }
        public int Accused { get; }
        public bool Correct { get; }
        /// <summary>Сколько взяток раскрыто (за каждую — штраф).</summary>
        public int Exposed { get; }

        public AccusationResult(int accuser, int accused, bool correct, int exposed)
        {
            Accuser = accuser;
            Accused = accused;
            Correct = correct;
            Exposed = exposed;
        }
    }

    /// <summary>
    /// Взятки и разоблачения. Взятку даёт текущий игрок тайно: в журнале все видят только «в мэрии кто-то
    /// шепчется», без имени. Следы взятки держатся до начала следующего хода давшего — до тех пор любой
    /// соперник в свой ход может обвинить его. Угадал — давший платит штраф за каждую раскрытую взятку,
    /// а обвинитель получает влияние; ошибся — штраф платит обвинитель. Улики — то, что видно всем:
    /// рука выросла сильнее обычного, дорога обошлась дешевле.
    /// </summary>
    public sealed partial class Game
    {
        sealed class BribeRecord
        {
            public int Player;
            public BribeKind Kind;
        }

        readonly List<BribeRecord> _bribes = new List<BribeRecord>();
        bool _bribedThisTurn;
        bool _accusedThisTurn;
        bool _discountActive;

        /// <summary>Сколько взяток сейчас «на виду» (их можно раскрыть). Число открытое: каждая взятка объявляется шёпотом.</summary>
        public int ActiveBribes => _bribes.Count;
        /// <summary>Растёт с каждой взяткой — интерфейс показывает «кто-то шепчется».</summary>
        public int WhisperNumber { get; private set; }
        public bool BribedThisTurn => _bribedThisTurn;
        public bool AccusedThisTurn => _accusedThisTurn;
        /// <summary>У текущего игрока в этот ход скидка от взятки (видно только ему в интерфейсе).</summary>
        public bool BribeDiscountActive => _discountActive;
        public AccusationResult LastAccusation { get; private set; }
        public int AccusationNumber { get; private set; }

        public string GetBribeBlocker(BribeKind kind)
        {
            if (!Config.BribesEnabled || !Config.InfluenceEnabled) return "Взятки выключены.";
            if (Phase != GamePhase.AwaitingAction) return "Взятку дают в начале хода, до основного действия.";
            if (_bribedThisTurn) return "Одна взятка за ход.";
            int cost = BribeInfo.Cost(kind, Config);
            if (CurrentPlayer.Influence < cost) return $"Нужно влияния: {cost}, у вас {CurrentPlayer.Influence}.";
            if (kind == BribeKind.ExtraCard && _deck.AvailableCount == 0) return "Колода пуста.";
            return null;
        }

        ActionResult DoBribe(BribeKind kind)
        {
            var blocker = GetBribeBlocker(kind);
            if (blocker != null) return ActionResult.Fail(blocker);
            var player = CurrentPlayer;
            int cost = BribeInfo.Cost(kind, Config);
            player.Influence -= cost;
            player.InfluenceSpent += cost;
            player.BribesGiven++;
            _bribedThisTurn = true;
            _bribes.Add(new BribeRecord { Player = player.Index, Kind = kind });
            if (kind == BribeKind.Discount) _discountActive = true;
            else if (_deck.TryDraw(out var card)) player.AddCard(card);
            WhisperNumber++;
            Emit("В мэрии кто-то шепчется: дана взятка. Её можно раскрыть, пока давший не начал свой следующий ход.");
            Changed();
            return ActionResult.Ok();
        }

        public string GetAccuseBlocker(int accused)
        {
            if (!Config.BribesEnabled || !Config.InfluenceEnabled) return "Взятки выключены.";
            if (Phase != GamePhase.AwaitingAction) return "Обвинить можно в начале своего хода.";
            if (_accusedThisTurn) return "Одно обвинение за ход.";
            if (accused < 0 || accused >= _players.Count || accused == CurrentPlayerIndex) return "Выберите соперника.";
            return null;
        }

        ActionResult DoAccuse(int accused)
        {
            var blocker = GetAccuseBlocker(accused);
            if (blocker != null) return ActionResult.Fail(blocker);
            _accusedThisTurn = true;
            var accuser = CurrentPlayer;
            var suspect = _players[accused];
            int exposed = _bribes.RemoveAll(b => b.Player == accused);
            if (exposed > 0)
            {
                int fine = Config.BribeFine * exposed;
                suspect.PenaltyPoints -= fine;
                suspect.BribesExposed += exposed;
                EarnInfluence(accuser, Config.BribeWhistleReward, "разоблачение");
                Emit($"{accuser.Name} обвиняет {suspect.Name} во взятке — подтвердилось! Раскрыто: {exposed}, штраф −{fine}.");
            }
            else
            {
                accuser.PenaltyPoints -= Config.FalseAccusationFine;
                Emit($"{accuser.Name} обвиняет {suspect.Name} во взятке — не подтвердилось. Ложное обвинение: −{Config.FalseAccusationFine}.");
            }
            LastAccusation = new AccusationResult(accuser.Index, accused, exposed > 0, exposed);
            AccusationNumber++;
            Changed();
            return ActionResult.Ok();
        }

        /// <summary>Конец хода: сброс флагов; у игрока, чей ход начинается, следы его взяток пропадают.</summary>
        void BribeTurnPassed()
        {
            _bribedThisTurn = false;
            _accusedThisTurn = false;
            _discountActive = false;
            _bribes.RemoveAll(b => b.Player == CurrentPlayerIndex);
        }

        // Улики — только то, что видно всем: насколько выросла рука за ход и не обошлась ли дорога дешевле.
        int _turnHandStart;
        bool _turnPaidLess;
        bool[] _suspicious;

        void RememberTurnEvidence(PlayerState player)
        {
            if (_suspicious == null || _suspicious.Length != _players.Count) _suspicious = new bool[_players.Count];
            _suspicious[player.Index] = player.HandSize - _turnHandStart >= 3 || _turnPaidLess;
            _turnPaidLess = false;
        }

        /// <summary>
        /// Выглядел ли прошлый ход игрока подозрительно по открытым данным (рука выросла на 3+ карты или дорога
        /// дешевле обычного). Это не доказательство: так бывает и после сделки или «Поставки».
        /// </summary>
        public bool LooksSuspicious(int player) => _suspicious != null && player >= 0 && player < _suspicious.Length && _suspicious[player];

        int ApplyBribeDiscount(int cost, PlayerState player) =>
            _discountActive && player != null && player.Index == CurrentPlayerIndex ? Math.Max(1, cost - 1) : cost;

        /// <summary>Есть ли сейчас нераскрытая взятка этого игрока (для тестов и бота-обвинителя не используется).</summary>
        internal bool Debug_HasBribe(int player) => _bribes.Any(b => b.Player == player);
    }
}
