using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Core
{
    /// <summary>
    /// Правила сценариев, которым нужно своё состояние (плановый ремонт «Года ремонта»), и выбор компании в начале
    /// партии: тот, кому персонаж не задан заранее, выбирает из Config.CharacterOffer случайных — вместе со
    /// стартовыми контрактами, уже зная их.
    /// </summary>
    public sealed partial class Game
    {
        readonly HashSet<string> _scheduledRepairs = new HashSet<string>();
        readonly List<string> _nextRepairs = new List<string>();

        public ScenarioKind Scenario => Scenarios.Of(Config);

        /// <summary>Год ремонта: участки, закрытые в этом круге, и те, что закроют в следующем.</summary>
        public IReadOnlyCollection<string> ScheduledRepairRoads => _scheduledRepairs;
        public IReadOnlyList<string> NextRepairRoads => _nextRepairs;
        public bool IsScheduledRepair(string roadId) => _scheduledRepairs.Contains(roadId);
        public bool IsNextRepair(string roadId) => _nextRepairs.Contains(roadId);

        List<string> FreeOpenRoads() => Map.Roads
            .Where(r => !_roadOwners.ContainsKey(r.Id) && IsRoadOpen(r.Id))
            .Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

        /// <summary>
        /// Начало круга в «Годе ремонта»: обещанные участки закрываются (если ещё свободны; недостающие добираются
        /// случайно), и выбираются участки на следующий круг.
        /// </summary>
        void RotateScheduledRepairs()
        {
            _scheduledRepairs.Clear();
            int count = Config.ScheduledRepairs;
            if (count <= 0)
            {
                _nextRepairs.Clear();
                return;
            }
            var free = FreeOpenRoads();
            var current = _nextRepairs.Where(free.Contains).ToList();
            var rest = free.Where(id => !current.Contains(id)).ToList();
            ShuffleUtil.InPlace(rest, _rng);
            while (current.Count < count && rest.Count > 0)
            {
                current.Add(rest[rest.Count - 1]);
                rest.RemoveAt(rest.Count - 1);
            }
            foreach (var id in current) _scheduledRepairs.Add(id);

            _nextRepairs.Clear();
            var next = rest.Take(count).OrderBy(id => id, StringComparer.Ordinal);
            _nextRepairs.AddRange(next);
            if (_scheduledRepairs.Count > 0)
                Emit("Год ремонта: в этом круге закрыты " +
                     string.Join(", ", _scheduledRepairs.OrderBy(id => id, StringComparer.Ordinal).Select(RoadName)) +
                     (_nextRepairs.Count > 0 ? ". В следующем закроют " + string.Join(", ", _nextRepairs.Select(RoadName)) + "." : "."));
        }

        string RoadName(string roadId)
        {
            var road = Map.GetRoad(roadId);
            return road == null ? roadId : $"{Map.LocationName(road.A)} — {Map.LocationName(road.B)}";
        }

        // ================================================================== Выбор компании

        /// <summary>
        /// Раздать выбирающим по CharacterOffer разных персонажей: сначала из тех, кого никто не взял заранее,
        /// по возможности без повторов между игроками; если не хватает — из всех.
        /// </summary>
        void DealCharacterOffers(List<PlayerState> drafting)
        {
            if (drafting.Count == 0) return;
            int size = Math.Max(1, Math.Min(Config.CharacterOffer, Characters.All.Count));
            var taken = new HashSet<CharacterKind>(_players.Where(p => p.Character != CharacterKind.None).Select(p => p.Character));
            var pool = Characters.All.Where(k => !taken.Contains(k)).ToList();
            ShuffleUtil.InPlace(pool, _rng);
            foreach (var player in drafting)
            {
                while (player.CharacterOfferList.Count < size)
                {
                    if (pool.Count == 0)
                    {
                        pool = Characters.All.Where(k => !player.CharacterOfferList.Contains(k)).ToList();
                        ShuffleUtil.InPlace(pool, _rng);
                        if (pool.Count == 0) break;
                    }
                    var pick = pool[pool.Count - 1];
                    pool.RemoveAt(pool.Count - 1);
                    if (!player.CharacterOfferList.Contains(pick)) player.CharacterOfferList.Add(pick);
                }
            }
        }

        ActionResult DoChooseCharacter(int playerIndex, CharacterKind kind)
        {
            if (Phase != GamePhase.SetupContracts) return ActionResult.Fail("Компанию выбирают в начале партии.");
            if (playerIndex < 0 || playerIndex >= _players.Count) return ActionResult.Fail("Нет такого игрока.");
            var player = _players[playerIndex];
            if (player.CharacterOfferList.Count == 0) return ActionResult.Fail($"{player.Name} уже выбрал компанию.");
            if (!player.CharacterOfferList.Contains(kind)) return ActionResult.Fail("Этого персонажа нет среди предложенных.");

            player.Character = kind;
            if (kind == CharacterKind.Foreman) player.FreeRoadsLeft = Config.ForemanFreeRoads;
            player.CharacterOfferList.Clear();
            Emit($"{player.Name} выбирает персонажа: {Characters.Title(kind)}.");
            CheckSetupComplete();
            Changed();
            return ActionResult.Ok();
        }
    }
}
