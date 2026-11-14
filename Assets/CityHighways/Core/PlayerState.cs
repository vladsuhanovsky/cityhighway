using System;
using System.Collections.Generic;

namespace CityHighways.Core
{
    /// <summary>Состояние игрока (дорожной компании). Снаружи только читается; меняет его Game.</summary>
    public sealed class PlayerState
    {
        readonly int[] _hand = new int[ResourceInfo.Count];
        readonly List<Road> _roads = new List<Road>();
        readonly List<Contract> _contracts = new List<Contract>();
        readonly List<Contract> _pending = new List<Contract>();

        public int Index { get; }
        public string Name { get; }
        /// <summary>Сколько участков дороги ещё можно построить.</summary>
        public int RoadPieces { get; internal set; }
        /// <summary>Очки за построенные дороги (контракты, плата за проезд и бонус — в конце).</summary>
        public int RoadPoints { get; internal set; }
        /// <summary>Очки за выполненные первым общие цели мэрии.</summary>
        public int GoalPoints { get; internal set; }
        /// <summary>Очки карт удачи (гранты, премии, штрафы); может быть меньше нуля.</summary>
        public int LuckPoints { get; internal set; }
        /// <summary>Сколько бафов и дебафов вытянул игрок — для сводки в итогах.</summary>
        public int LuckBuffs { get; internal set; }
        public int LuckDebuffs { get; internal set; }

        public IReadOnlyList<Road> Roads => _roads;
        public IReadOnlyList<Contract> Contracts => _contracts;
        /// <summary>Контракты, из которых игрок сейчас выбирает.</summary>
        public IReadOnlyList<Contract> PendingContracts => _pending;

        public int HandSize
        {
            get
            {
                int sum = 0;
                foreach (int n in _hand) sum += n;
                return sum;
            }
        }

        /// <summary>Влияние сейчас, всего заработано и потрачено за партию.</summary>
        public int Influence { get; internal set; }
        public int InfluenceEarned { get; internal set; }
        public int InfluenceSpent { get; internal set; }
        /// <summary>Контракты, за которые влияние уже начислено, и районы, где у компании есть дороги.</summary>
        internal readonly HashSet<string> RewardedContracts = new HashSet<string>();
        internal readonly HashSet<string> InfluenceDistricts = new HashSet<string>();

        /// <summary>Контракт, выигранный на аукционе мэрии (бонус в конце, если выполнен); null — нет.</summary>
        public string MayorContractId { get; internal set; }

        /// <summary>Штрафы за раскрытые взятки и ложные обвинения (≤ 0).</summary>
        public int PenaltyPoints { get; internal set; }
        public int BribesGiven { get; internal set; }
        public int BribesExposed { get; internal set; }

        /// <summary>
        /// Проезд, оплаченный по ходу партии (маршрут фиксируется, когда контракт выполнен): заплачено соперникам,
        /// получено от соперников и сгорело в пробках.
        /// </summary>
        public int TollsPaid { get; internal set; }
        public int TollsEarned { get; internal set; }
        public int JamFees { get; internal set; }
        /// <summary>Зафиксированные маршруты выполненных контрактов (по Id контракта).</summary>
        internal readonly Dictionary<string, PaidRoute> PaidRoutes = new Dictionary<string, PaidRoute>();
        public IReadOnlyDictionary<string, PaidRoute> PaidRouteList => PaidRoutes;
        /// <summary>Чьи дороги для этой компании бесплатны (пропуска, купленные в сделках).</summary>
        internal readonly HashSet<int> PassesFrom = new HashSet<int>();
        public IEnumerable<int> Passes => PassesFrom;

        /// <summary>Персонаж компании.</summary>
        public CharacterKind Character { get; internal set; }
        /// <summary>Сколько бесплатных строек осталось у Прораба.</summary>
        public int FreeRoadsLeft { get; internal set; }

        internal PlayerState(int index, string name, int roadPieces)
        {
            Index = index;
            Name = name;
            RoadPieces = roadPieces;
        }

        public int CardCount(Resource resource) => _hand[(int)resource];

        internal void AddCard(Resource resource, int count = 1) => _hand[(int)resource] += count;

        internal void RemoveCards(Resource resource, int count)
        {
            if (count > _hand[(int)resource])
                throw new InvalidOperationException($"У игрока {Name} нет {count} карт «{ResourceInfo.RussianName(resource)}».");
            _hand[(int)resource] -= count;
        }

        internal void ClearHand()
        {
            for (int i = 0; i < _hand.Length; i++) _hand[i] = 0;
        }

        internal void AddRoad(Road road) => _roads.Add(road);
        internal List<Contract> ContractList => _contracts;
        internal List<Contract> PendingList => _pending;

        public override string ToString() => Name;
    }
}
