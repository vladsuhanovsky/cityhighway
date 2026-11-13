using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    // Общие цели мэрии и колода удачи.
    public sealed partial class Game
    {
        readonly List<MayorGoal> _goals = new List<MayorGoal>();
        readonly Dictionary<string, int> _goalOwners = new Dictionary<string, int>();
        readonly List<LuckCardKind> _luckDeck = new List<LuckCardKind>();    // последний элемент — верхняя карта
        readonly List<LuckCardKind> _luckDiscard = new List<LuckCardKind>();
        bool _luckTakenThisTurn;
        readonly List<LuckCardKind> _luckChoice = new List<LuckCardKind>();
        bool _luckChoiceIsSecondCard;

        /// <summary>Две карты, из которых выбирает Везунчик (фаза ChoosingLuck); иначе пусто.</summary>
        public IReadOnlyList<LuckCardKind> PendingLuckChoice => _luckChoice;

        /// <summary>Общие цели мэрии этой партии.</summary>
        public IReadOnlyList<MayorGoal> ActiveGoals => _goals;

        /// <summary>Кто первым выполнил цель; null — цель ещё свободна.</summary>
        public int? GoalOwner(string goalId) => _goalOwners.TryGetValue(goalId, out var owner) ? owner : (int?)null;

        public int GoalProgress(int playerIndex, MayorGoal goal) => goal.Progress(Map, _players[playerIndex].Roads);

        /// <summary>Сколько карт удачи осталось до перемешивания.</summary>
        public int LuckDeckCount => _luckDeck.Count;
        public int LuckDiscardCount => _luckDiscard.Count;
        /// <summary>
        /// Сколько бафов и дебафов осталось в колоде удачи (порядок карт не раскрывается).
        /// Состав колоды открыт, вышедшие карты видны всем — это обычный подсчёт карт.
        /// Если колода пуста, следующая карта возьмётся из перемешанного сброса — тогда считается сброс.
        /// </summary>
        public int LuckBuffsLeft => LuckPool.Count(LuckInfo.IsBuff);
        public int LuckDebuffsLeft => LuckPool.Count(k => !LuckInfo.IsBuff(k));
        /// <summary>Будет ли перед следующей картой удачи перемешан сброс.</summary>
        public bool LuckWillReshuffle => _luckDeck.Count == 0 && _luckDiscard.Count > 0;

        List<LuckCardKind> LuckPool => _luckDeck.Count > 0 ? _luckDeck : _luckDiscard;
        public bool LuckTakenThisTurn => _luckTakenThisTurn;

        /// <summary>Последняя открытая карта удачи, кто её взял и что получилось — для интерфейса.</summary>
        public LuckCardKind? LastLuckCard { get; private set; }
        public int LastLuckPlayer { get; private set; } = -1;
        public string LastLuckResult { get; private set; }
        /// <summary>Сколько карт удачи уже открыто; интерфейс по нему замечает новую.</summary>
        public int LuckNumber { get; private set; }

        /// <summary>
        /// Можно ли сейчас взять карту удачи: она заменяет одну из двух карт ресурсов,
        /// поэтому нужна хотя бы одна доступная карта ресурса, и берётся не больше одной за ход.
        /// </summary>
        public bool CanDrawLuckCard() =>
            Config.LuckEnabled
            && (Phase == GamePhase.AwaitingAction || Phase == GamePhase.DrawingSecondCard)
            && !_luckTakenThisTurn
            && _luckDeck.Count + _luckDiscard.Count > 0
            && CanDrawAnyCard();

        ActionResult DoDrawLuckCard()
        {
            if (!Config.LuckEnabled) return ActionResult.Fail("Колода удачи выключена.");
            if (Phase != GamePhase.AwaitingAction && Phase != GamePhase.DrawingSecondCard)
                return ActionResult.Fail("Сейчас нельзя брать карты.");
            if (_luckTakenThisTurn) return ActionResult.Fail("Карту удачи можно взять только одну за ход.");
            if (_luckDeck.Count + _luckDiscard.Count == 0) return ActionResult.Fail("Колода удачи пуста.");
            if (!CanDrawAnyCard()) return ActionResult.Fail("Карта удачи берётся вместо карты ресурса, а их не осталось.");

            if (_luckDeck.Count == 0)
            {
                _luckDeck.AddRange(_luckDiscard);
                _luckDiscard.Clear();
                ShuffleUtil.InPlace(_luckDeck, _rng);
            }
            var kind = TakeLuckTop();
            _luckTakenThisTurn = true;
            bool isSecond = Phase == GamePhase.DrawingSecondCard;
            var player = CurrentPlayer;

            // Везунчик открывает вторую карту (если в колоде она есть, без перемешивания) и выбирает.
            if (player.Character == CharacterKind.Lucky && _luckDeck.Count > 0)
            {
                _luckChoice.Clear();
                _luckChoice.Add(kind);
                _luckChoice.Add(TakeLuckTop());
                _luckChoiceIsSecondCard = isSecond;
                Phase = GamePhase.ChoosingLuck;
                Emit($"{player.Name} открывает две карты удачи: «{LuckInfo.Title(_luckChoice[0])}» и «{LuckInfo.Title(_luckChoice[1])}».");
                Changed();
                return ActionResult.Ok();
            }

            ResolveLuck(player, kind, isSecond);
            return ActionResult.Ok();
        }

        /// <summary>Везунчик выбирает одну из двух открытых карт удачи (0 или 1). Обе уже в сбросе.</summary>
        ActionResult DoChooseLuckCard(int index)
        {
            if (Phase != GamePhase.ChoosingLuck) return ActionResult.Fail("Сейчас не нужно выбирать карту удачи.");
            if (index < 0 || index >= _luckChoice.Count) return ActionResult.Fail("Нет такой карты.");
            var kind = _luckChoice[index];
            _luckChoice.Clear();
            ResolveLuck(CurrentPlayer, kind, _luckChoiceIsSecondCard);
            return ActionResult.Ok();
        }

        LuckCardKind TakeLuckTop()
        {
            int top = _luckDeck.Count - 1;
            var kind = _luckDeck[top];
            _luckDeck.RemoveAt(top);
            _luckDiscard.Add(kind);
            return kind;
        }

        void ResolveLuck(PlayerState player, LuckCardKind kind, bool isSecondCard)
        {
            if (LuckInfo.IsBuff(kind)) player.LuckBuffs++;
            else player.LuckDebuffs++;
            string result = ApplyLuck(player, kind);
            LastLuckCard = kind;
            LastLuckPlayer = player.Index;
            LastLuckResult = result;
            LuckNumber++;
            Emit($"{player.Name} берёт карту удачи «{LuckInfo.Title(kind)}»: {result}");

            if (isSecondCard || !CanDrawSecondCard()) EndTurn();
            else
            {
                Phase = GamePhase.DrawingSecondCard;
                Changed();
            }
        }

        /// <summary>
        /// Насколько карта удачи хороша для игрока прямо сейчас (в очках, примерно) — так выбирает бот-Везунчик.
        /// Повторяет luck_value из Python-модели.
        /// </summary>
        public double LuckValue(PlayerState player, LuckCardKind kind)
        {
            if (!LuckInfo.IsBuff(kind) && player.Character == CharacterKind.Insurer) return -Config.InsurerFine;
            switch (kind)
            {
                case LuckCardKind.Grant: return Config.InfluenceEnabled ? Config.LuckGrantInfluence : Config.LuckGrant;
                case LuckCardKind.Supply: return 1.5 * Config.LuckSupply;
                case LuckCardKind.Premium:
                    return Math.Min(Config.LuckPremiumMax,
                        Config.LuckPremiumPerContract * player.Contracts.Count(c => CheckContract(player.Index, c).Completed));
                case LuckCardKind.Fine: return -Config.LuckFine;
                case LuckCardKind.Theft: return -4.0 * Math.Min(Config.LuckTheft, player.HandSize);
                default:
                {
                    var open = player.Contracts.Where(c => !CheckContract(player.Index, c).Completed).ToList();
                    return open.Count > 0 ? -0.6 * open.Min(c => c.Points) : -Config.LuckFine;
                }
            }
        }

        /// <summary>Применить карту удачи; возвращает, что получилось, для журнала.</summary>
        string ApplyLuck(PlayerState player, LuckCardKind kind)
        {
            if (!LuckInfo.IsBuff(kind) && player.Character == CharacterKind.Insurer)
            {
                player.LuckPoints -= Config.InsurerFine;
                return $"страховка: вместо дебафа −{Config.InsurerFine} очко.";
            }
            switch (kind)
            {
                case LuckCardKind.Grant:
                    if (Config.InfluenceEnabled)
                    {
                        player.Influence += Config.LuckGrantInfluence;
                        player.InfluenceEarned += Config.LuckGrantInfluence;
                        return $"+{Config.LuckGrantInfluence} влияния.";
                    }
                    player.LuckPoints += Config.LuckGrant;
                    return $"+{Config.LuckGrant} очков.";

                case LuckCardKind.Supply:
                {
                    int got = 0;
                    for (int i = 0; i < Config.LuckSupply; i++)
                    {
                        if (!_deck.TryDraw(out var card)) break;
                        player.AddCard(card);
                        got++;
                    }
                    return $"+{got} карт ресурсов.";
                }

                case LuckCardKind.Premium:
                {
                    int done = player.Contracts.Count(c => CheckContract(player.Index, c).Completed);
                    int points = Math.Min(Config.LuckPremiumMax, Config.LuckPremiumPerContract * done);
                    player.LuckPoints += points;
                    return $"выполнено контрактов: {done}, +{points} очков.";
                }

                case LuckCardKind.Fine:
                    player.LuckPoints -= Config.LuckFine;
                    return $"−{Config.LuckFine} очка.";

                case LuckCardKind.Theft:
                {
                    int lost = 0;
                    for (int i = 0; i < Config.LuckTheft; i++)
                    {
                        var cards = new List<Resource>();
                        for (int r = 0; r < ResourceInfo.Count; r++)
                            for (int k = 0; k < player.CardCount((Resource)r); k++) cards.Add((Resource)r);
                        if (cards.Count == 0) break;
                        var card = cards[_rng.Next(cards.Count)];
                        player.RemoveCards(card, 1);
                        _deck.Discard(card);
                        lost++;
                    }
                    return lost > 0 ? $"−{lost} карта с руки." : "рука пуста, красть нечего.";
                }

                default: // Recall
                {
                    var open = player.Contracts.Where(c => !CheckContract(player.Index, c).Completed).ToList();
                    if (open.Count == 0)
                    {
                        player.LuckPoints -= Config.LuckFine;
                        return $"невыполненных контрактов нет, −{Config.LuckFine} очка.";
                    }
                    var contract = open.OrderBy(c => c.Points).ThenBy(c => c.Id, StringComparer.Ordinal).First();
                    player.ContractList.Remove(contract);
                    _contractDeck.ReturnToBottom(new[] { contract });
                    return $"контракт {Map.DistrictName(contract.DistrictA)} — {Map.DistrictName(contract.DistrictB)} ({contract.Points}) уходит под колоду.";
                }
            }
        }

        /// <summary>После постройки: забрать награды за выполненные первым цели.</summary>
        void CheckGoals(PlayerState player)
        {
            foreach (var goal in _goals)
            {
                if (_goalOwners.ContainsKey(goal.Id)) continue;
                if (!goal.IsDone(Map, player.Roads)) continue;
                _goalOwners[goal.Id] = player.Index;
                player.GoalPoints += goal.Points;
                Emit($"{player.Name} первым выполняет цель мэрии «{goal.Name}» (+{goal.Points}).");
                EarnInfluence(player, Config.InfluencePerGoal, "цель мэрии");
            }
        }

        /// <summary>В начале партии: открыть цели мэрии и перемешать колоду удачи.</summary>
        void SetupGoalsAndLuck()
        {
            var pool = Map.Goals.ToList();
            ShuffleUtil.InPlace(pool, _rng);
            _goals.AddRange(pool.Take(Math.Max(0, Config.MayorGoalsDealt)));

            if (!Config.LuckEnabled) return;
            for (int k = 0; k < LuckInfo.KindCount; k++)
                for (int i = 0; i < LuckInfo.CountIn(Config, (LuckCardKind)k); i++) _luckDeck.Add((LuckCardKind)k);
            ShuffleUtil.InPlace(_luckDeck, _rng);
        }

        // ================================================================== Для тестов

        /// <summary>Положить карту удачи на верх колоды.</summary>
        internal void Debug_PutLuckOnTop(LuckCardKind kind) => _luckDeck.Add(kind);

        /// <summary>Заменить цели партии.</summary>
        internal void Debug_SetGoals(params string[] goalIds)
        {
            _goals.Clear();
            _goalOwners.Clear();
            foreach (var id in goalIds) _goals.Add(Map.GetGoal(id));
        }

        internal void Debug_CheckGoals(int playerIndex) => CheckGoals(_players[playerIndex]);

        /// <summary>Разрешить ещё одну карту удачи и вернуть фазу выбора первой карты (без смены хода).</summary>
        /// <summary>Положить две карты удачи на верх колоды: first будет взята первой.</summary>
        internal void Debug_PutLuckOnTop(LuckCardKind first, LuckCardKind second)
        {
            _luckDeck.Add(second);
            _luckDeck.Add(first);
        }

        internal void Debug_ResetLuckTurn()
        {
            _luckTakenThisTurn = false;
            if (Phase == GamePhase.DrawingSecondCard) Phase = GamePhase.AwaitingAction;
        }
    }
}
