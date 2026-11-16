using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>Заказ города в кооперативе: контракт, общий для всей команды, и сколько кругов на него осталось.</summary>
    public sealed class CityOrder
    {
        public Contract Contract { get; }
        public int RoundsLeft { get; internal set; }

        public CityOrder(Contract contract, int roundsLeft)
        {
            Contract = contract;
            RoundsLeft = roundsLeft;
        }
    }

    /// <summary>
    /// Кооператив «Город против всех». Все компании — одна команда:
    /// <list type="bullet">
    /// <item>сеть общая — дорога любого союзника засчитывается в контракты, проезд между союзниками бесплатный;</item>
    /// <item>на столе открыто CoopOpenOrders заказов города; заказ выполнен, когда общая сеть связывает его районы
    /// (очки получает тот, чья дорога замкнула путь, кризис снижается);</item>
    /// <item>у заказа таймер в кругах; просроченный заказ поднимает кризис;</item>
    /// <item>город растёт: кризис +1 каждые CoopCrisisEveryRounds кругов, ремонт, снегопад и парад добавляют ещё;</item>
    /// <item>победа — выполнить CoopOrdersToWin заказов; поражение — кризис дошёл до CoopCrisisMax
    /// или участки кончились раньше.</item>
    /// </list>
    /// Всё детерминировано (заказы берутся сверху колоды контрактов), так что сохранение и сеть воспроизводят партию точно.
    /// </summary>
    public sealed partial class Game
    {
        readonly List<CityOrder> _cityOrders = new List<CityOrder>();
        int _coopRounds;

        public bool IsCooperative => Config.Cooperative;
        public IReadOnlyList<CityOrder> CityOrders => _cityOrders;
        /// <summary>Контракты открытых заказов — для ботов и подсветки.</summary>
        public IEnumerable<Contract> CityOrderContracts => _cityOrders.Select(o => o.Contract);
        public int Crisis { get; private set; }
        public int CrisisMax => Config.CoopCrisisMax;
        public int OrdersDone { get; private set; }
        public int OrdersFailed { get; private set; }
        public int OrdersTarget => Config.CoopOrdersToWin(_players.Count);
        /// <summary>Итог кооператива: null — партия идёт; иначе причина победы или поражения.</summary>
        public string CoopOutcome { get; private set; }
        public bool CoopWon { get; private set; }
        /// <summary>Растёт при каждой новости кооператива (заказ выполнен или просрочен) — интерфейс показывает всплывашку.</summary>
        public int CoopNewsNumber { get; private set; }
        public string LastCoopNews { get; private set; }
        public bool LastCoopNewsGood { get; private set; }

        /// <summary>
        /// Владелец дороги «с точки зрения контрактов игрока»: в кооперативе любая построенная дорога — своя
        /// (общая сеть, без платы и без предела чужих дорог), иначе — настоящий владелец.
        /// </summary>
        Func<string, int?> ContractOwnerFor(int playerIndex)
        {
            if (!IsCooperative) return GetRoadOwner;
            return id => id != null && _roadOwners.ContainsKey(id) ? playerIndex : (int?)null;
        }

        /// <summary>Связывает ли общая сеть команды районы контракта.</summary>
        public bool TeamCompleted(Contract contract) =>
            contract != null && Scoring.CheckContract(Map, contract, 0, ContractOwnerFor(0),
                Config.MaxForeignRoadsPerContract, _ => 0).Completed;

        /// <summary>Подготовка кооператива при создании партии (после раздачи стартовых контрактов).</summary>
        void CoopSetup()
        {
            if (!IsCooperative) return;
            Crisis = Config.CoopCrisisStart;
            RefillCityOrders(true);
        }

        void RefillCityOrders(bool quiet)
        {
            int guard = 0;
            while (_cityOrders.Count < Config.CoopOpenOrders && _contractDeck.Count > 0 && guard++ < 60)
            {
                var contract = _contractDeck.Draw(1).FirstOrDefault();
                if (contract == null) break;
                // Уже выполненный общей сетью заказ — не заказ: под колоду.
                if (TeamCompleted(contract))
                {
                    _contractDeck.ReturnToBottom(new[] { contract });
                    continue;
                }
                _cityOrders.Add(new CityOrder(contract, Config.CoopOrderRounds));
                if (!quiet) Emit($"Новый заказ города: {DescribeOrder(contract)} — {contract.Points} очк., срок {Config.CoopOrderRounds} кругов.");
            }
        }

        string DescribeOrder(Contract contract)
        {
            string text = $"{Map.DistrictName(contract.DistrictA)} — {Map.DistrictName(contract.DistrictB)}";
            if (contract.IsComplex) text += (contract.Kind == ContractKind.Transit ? " через " : " и ") + Map.DistrictName(contract.DistrictC);
            return text;
        }

        /// <summary>
        /// После стройки: выполнены ли заказы. Очки за заказ — тому, чья дорога замкнула путь (в столбец «Цели»).
        /// true — набрано нужное число заказов, партия выиграна и закончена.
        /// </summary>
        bool CoopCheckOrders(PlayerState builder)
        {
            if (!IsCooperative || Phase == GamePhase.GameOver) return false;
            bool any = false;
            foreach (var order in _cityOrders.ToList())
            {
                if (!TeamCompleted(order.Contract)) continue;
                _cityOrders.Remove(order);
                OrdersDone++;
                builder.GoalPoints += order.Contract.Points;
                int before = Crisis;
                Crisis = Math.Max(0, Crisis - Config.CoopCrisisReliefPerOrder);
                News($"Заказ города выполнен: {DescribeOrder(order.Contract)} (+{order.Contract.Points} очк. — {builder.Name}). " +
                     $"Кризис {before} → {Crisis}. Выполнено {OrdersDone} из {OrdersTarget}.", true);
                any = true;
            }
            if (OrdersDone >= OrdersTarget)
            {
                CoopFinish(true, $"Город спасён: выполнено {OrdersDone} заказов из {OrdersTarget}, кризис {Crisis} из {CrisisMax}.");
                return true;
            }
            if (any) RefillCityOrders(false);
            return false;
        }

        /// <summary>
        /// Начало круга: таймеры заказов, просрочки, рост города. true — кризис дошёл до предела, партия проиграна.
        /// </summary>
        bool CoopRoundStart()
        {
            if (!IsCooperative || Phase == GamePhase.GameOver) return false;
            _coopRounds++;
            if (_coopRounds <= 1) return false; // первый круг только начался — время ещё не шло

            foreach (var order in _cityOrders.ToList())
            {
                order.RoundsLeft--;
                if (order.RoundsLeft > 0) continue;
                _cityOrders.Remove(order);
                OrdersFailed++;
                Crisis += Config.CoopCrisisPerFailedOrder;
                News($"Заказ города просрочен: {DescribeOrder(order.Contract)}. Кризис +{Config.CoopCrisisPerFailedOrder} → {Crisis} из {CrisisMax}.", false);
            }
            if (Config.CoopCrisisEveryRounds > 0 && _coopRounds % Config.CoopCrisisEveryRounds == 0)
            {
                Crisis++;
                Emit($"Город растёт: кризис +1 → {Crisis} из {CrisisMax}.");
            }
            if (Crisis >= CrisisMax)
            {
                CoopFinish(false, $"Город встал: кризис дошёл до {CrisisMax}. Выполнено заказов: {OrdersDone} из {OrdersTarget}.");
                return true;
            }
            RefillCityOrders(false);
            return false;
        }

        /// <summary>Плохие события давят на город: ремонт, снегопад, парад. true — кризис дошёл до предела.</summary>
        bool CoopOnEvent(CityEventKind kind)
        {
            if (!IsCooperative || Phase == GamePhase.GameOver || Config.CoopCrisisPerBadEvent <= 0) return false;
            if (kind != CityEventKind.Repair && kind != CityEventKind.Snow && kind != CityEventKind.Parade) return false;
            Crisis += Config.CoopCrisisPerBadEvent;
            Emit($"Событие давит на город: кризис +{Config.CoopCrisisPerBadEvent} → {Crisis} из {CrisisMax}.");
            if (Crisis < CrisisMax) return false;
            CoopFinish(false, $"Город встал: кризис дошёл до {CrisisMax}. Выполнено заказов: {OrdersDone} из {OrdersTarget}.");
            return true;
        }

        void News(string text, bool good)
        {
            LastCoopNews = text;
            LastCoopNewsGood = good;
            CoopNewsNumber++;
            Emit(text);
        }

        void CoopFinish(bool won, string reason)
        {
            CoopWon = won;
            CoopOutcome = reason;
            FinishGame();
        }

        /// <summary>Итог кооператива, если партия закончилась сама (участки кончились, ходов нет).</summary>
        void CoopResolveNaturalEnd()
        {
            if (!IsCooperative || CoopOutcome != null) return;
            CoopWon = OrdersDone >= OrdersTarget;
            CoopOutcome = CoopWon
                ? $"Город спасён: выполнено {OrdersDone} заказов из {OrdersTarget}."
                : $"Участки кончились раньше, чем город получил свои заказы: выполнено {OrdersDone} из {OrdersTarget}.";
        }

        // ---------------------------------------------------------------- Сохранение

        internal void CoopFillSave(GameSaveData data)
        {
            if (!IsCooperative) return;
            data.coopOrders = _cityOrders.Select(o => o.Contract.Id).ToArray();
            data.coopRoundsLeft = _cityOrders.Select(o => o.RoundsLeft).ToArray();
            data.coopCrisis = Crisis;
            data.coopDone = OrdersDone;
            data.coopFailed = OrdersFailed;
            data.coopRounds = _coopRounds;
        }

        internal void CoopRestore(GameSaveData data)
        {
            if (!IsCooperative) return;
            _cityOrders.Clear();
            var ids = data.coopOrders ?? new string[0];
            for (int i = 0; i < ids.Length; i++)
            {
                var contract = Map.GetContract(ids[i]);
                if (contract == null) continue;
                int left = data.coopRoundsLeft != null && i < data.coopRoundsLeft.Length ? data.coopRoundsLeft[i] : Config.CoopOrderRounds;
                _cityOrders.Add(new CityOrder(contract, Math.Max(1, left)));
            }
            Crisis = data.coopCrisis;
            OrdersDone = data.coopDone;
            OrdersFailed = data.coopFailed;
            _coopRounds = data.coopRounds;
        }

        // ---------------------------------------------------------------- Для тестов

        internal void Debug_SetCrisis(int crisis) => Crisis = crisis;

        internal void Debug_SetOrderRounds(int rounds)
        {
            foreach (var order in _cityOrders) order.RoundsLeft = rounds;
        }
    }
}
