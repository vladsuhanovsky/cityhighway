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
    /// Взятки и разоблачения (только при BribesMinPlayers игроках и больше). Взятку даёт текущий игрок тайно:
    /// в момент взятки никто ничего не видит. В начале следующего круга объявляется только число взяток
    /// прошлого круга — без имён и без того, когда это было. Раскрыть взятку можно до конца круга после неё:
    /// любой в свой ход может обвинить любого соперника. Угадал — давший платит штраф (BribeFineFor) за каждую
    /// раскрытую взятку, а обвинитель получает влияние; ошибся — штраф платит обвинитель. Улики — то, что видно
    /// всем: рука за ход выросла на 3 карты и больше, дорога обошлась дешевле своей длины (так бывает и честно:
    /// сделка, «Поставка», субсидия, Мостовик).
    /// </summary>
    public sealed partial class Game
    {
        sealed class BribeRecord
        {
            public int Player;
            public BribeKind Kind;
            /// <summary>Круг, в котором дана взятка.</summary>
            public int Round;
        }

        /// <summary>Номер круга (растёт в начале каждого круга).</summary>
        public int RoundNumber { get; private set; }

        readonly List<BribeRecord> _bribes = new List<BribeRecord>();
        bool _bribedThisTurn;
        bool _accusedThisTurn;
        bool _discountActive;

        /// <summary>Все нераскрытые взятки — тайна, только для тестов.</summary>
        internal int ActiveBribes => _bribes.Count;
        /// <summary>Сколько взяток прошлого круга ещё не раскрыто — это число объявлено всем.</summary>
        public int AnnouncedBribes => BribesAllowed ? _bribes.Count(b => b.Round == RoundNumber - 1) : 0;
        /// <summary>Сколько взяток было объявлено в начале этого круга.</summary>
        public int LastWhisperCount { get; private set; }
        /// <summary>Растёт, когда в начале круга объявляют о взятках прошлого круга.</summary>
        public int WhisperNumber { get; private set; }
        /// <summary>Взятки вообще возможны в этой партии.</summary>
        public bool BribesAllowed => Config.BribesEnabled && Config.InfluenceEnabled && _players.Count >= Config.BribesMinPlayers;
        public bool BribedThisTurn => _bribedThisTurn;
        public bool AccusedThisTurn => _accusedThisTurn;
        /// <summary>У текущего игрока в этот ход скидка от взятки (видно только ему в интерфейсе).</summary>
        public bool BribeDiscountActive => _discountActive;
        public AccusationResult LastAccusation { get; private set; }
        public int AccusationNumber { get; private set; }

        public string GetBribeBlocker(BribeKind kind)
        {
            if (!Config.BribesEnabled || !Config.InfluenceEnabled) return "Взятки выключены.";
            if (!BribesAllowed) return $"Взятки — только когда игроков {Config.BribesMinPlayers} и больше.";
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
            _bribes.Add(new BribeRecord { Player = player.Index, Kind = kind, Round = RoundNumber });
            if (kind == BribeKind.Discount) _discountActive = true;
            else if (_deck.TryDraw(out var card)) player.AddCard(card);
            // В журнал ничего: о взятке узнают только в начале следующего круга — числом, без имени.
            Changed();
            return ActionResult.Ok();
        }

        public string GetAccuseBlocker(int accused)
        {
            if (!Config.BribesEnabled || !Config.InfluenceEnabled) return "Взятки выключены.";
            if (!BribesAllowed) return $"Взятки — только когда игроков {Config.BribesMinPlayers} и больше.";
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
                int fine = Config.BribeFineFor(_players.Count) * exposed;
                suspect.PenaltyPoints -= fine;
                suspect.BribesExposed += exposed;
                EarnInfluence(accuser, Config.BribeWhistleReward, "разоблачение");
                accuser.CorrectAccusations++;
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

        /// <summary>Конец хода: сброс флагов этого хода. Сами взятки живут до конца круга после них.</summary>
        void BribeTurnPassed()
        {
            _bribedThisTurn = false;
            _accusedThisTurn = false;
            _discountActive = false;
        }

        /// <summary>
        /// Начало круга: взятки позапрошлого круга раскрыть уже нельзя, о взятках прошлого объявляется числом.
        /// </summary>
        void AnnounceBribes()
        {
            RoundNumber++;
            _bribes.RemoveAll(b => b.Round < RoundNumber - 1);
            LastWhisperCount = AnnouncedBribes;
            if (LastWhisperCount == 0) return;
            WhisperNumber++;
            Emit($"В прошлом круге в мэрии шептались: взяток — {LastWhisperCount}. Кто — неизвестно; раскрыть можно до конца этого круга.");
        }

        // Улики — только то, что видно всем: насколько выросла рука за ход и не обошлась ли дорога дешевле длины.
        int _turnHandStart;
        bool _turnPaidLess;
        bool[] _suspicious;
        /// <summary>Чем подозрителен прошлый ход: SuspicionHand | SuspicionCheap.</summary>
        int[] _suspicionFlags;
        const int SuspicionHand = 1, SuspicionCheap = 2;

        void RememberTurnEvidence(PlayerState player)
        {
            if (_suspicious == null || _suspicious.Length != _players.Count) _suspicious = new bool[_players.Count];
            if (_suspicionFlags == null || _suspicionFlags.Length != _players.Count) _suspicionFlags = new int[_players.Count];
            int flags = (player.HandSize - _turnHandStart >= 3 ? SuspicionHand : 0) | (_turnPaidLess ? SuspicionCheap : 0);
            _suspicionFlags[player.Index] = flags;
            _suspicious[player.Index] = flags != 0;
            _turnPaidLess = false;
        }

        /// <summary>
        /// Выглядел ли прошлый ход игрока подозрительно по открытым данным (рука выросла на 3+ карты или дорога
        /// дешевле своей длины). Это не доказательство: так бывает и после сделки, «Поставки», субсидии, у Мостовика.
        /// </summary>
        public bool LooksSuspicious(int player) => _suspicious != null && player >= 0 && player < _suspicious.Length && _suspicious[player];

        /// <summary>Улики прошлого хода игрока словами; пусто — ничего подозрительного.</summary>
        public string SuspicionText(int player)
        {
            if (!LooksSuspicious(player)) return "";
            int flags = _suspicionFlags != null && player < _suspicionFlags.Length ? _suspicionFlags[player] : 0;
            var parts = new List<string>();
            if ((flags & SuspicionHand) != 0) parts.Add("рука выросла на 3+ карты");
            if ((flags & SuspicionCheap) != 0) parts.Add("дорога дешевле своей длины");
            return parts.Count > 0 ? string.Join(", ", parts) : "ход выглядел странно";
        }

        int ApplyBribeDiscount(int cost, PlayerState player) =>
            _discountActive && player != null && player.Index == CurrentPlayerIndex ? Math.Max(1, cost - 1) : cost;

        /// <summary>Есть ли сейчас нераскрытая взятка этого игрока (для тестов и бота-обвинителя не используется).</summary>
        internal bool Debug_HasBribe(int player) => _bribes.Any(b => b.Player == player);
    }
}
