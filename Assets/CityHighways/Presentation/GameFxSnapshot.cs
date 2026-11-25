using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>Detached presentation snapshot used only to compare consecutive game states for effects.</summary>
    public sealed class GameFxSnapshot
    {
        private GameFxSnapshot() { }

        public int[,] Hands;
        public Resource?[] FaceUp;
        public readonly Dictionary<string, int> Owners = new Dictionary<string, int>();
        public readonly HashSet<string> Completed = new HashSet<string>();
        public int Acting;
        public GamePhase Phase;
        public int ContractDeck;
        public int EventNumber;
        public int LuckNumber;
        public readonly Dictionary<string, int> GoalOwners = new Dictionary<string, int>();
        /// <summary>Сколько каждому заплатили за проезд и какие дороги в пробке.</summary>
        public int[] Earned;
        /// <summary>Видимые очки без проезда (дороги, цели, удача, штрафы) — для всплывающих «+N».</summary>
        public int[] Points;
        public readonly HashSet<string> Jammed = new HashSet<string>();
        public readonly HashSet<string> Highways = new HashSet<string>();

        public static GameFxSnapshot Capture(Game game)
        {
            var s = new GameFxSnapshot
            {
                Hands = new int[game.Players.Count, ResourceInfo.Count],
                FaceUp = game.FaceUpCards.ToArray(),
                Acting = game.ActingPlayerIndex,
                Phase = game.Phase,
                ContractDeck = game.ContractDeckCount,
                EventNumber = game.EventNumber,
                LuckNumber = game.LuckNumber,
                Earned = game.Players.Select(p => p.TollsEarned).ToArray(),
                Points = game.Players.Select(p => p.RoadPoints + p.GoalPoints + p.LuckPoints + p.PenaltyPoints).ToArray()
            };
            foreach (var goal in game.ActiveGoals)
            {
                var goalOwner = game.GoalOwner(goal.Id);
                if (goalOwner.HasValue) s.GoalOwners[goal.Id] = goalOwner.Value;
            }

            foreach (var player in game.Players)
            {
                for (int c = 0; c < ResourceInfo.Count; c++) s.Hands[player.Index, c] = player.CardCount((Resource)c);
                foreach (var contract in player.Contracts)
                    if (game.CheckContract(player.Index, contract).Completed) s.Completed.Add(player.Index + ":" + contract.Id);
            }

            foreach (var route in game.Map.Roads)
            {
                var owner = game.GetRoadOwner(route.Id);
                if (owner.HasValue) s.Owners[route.Id] = owner.Value;
                if (owner.HasValue && game.IsJammed(route.Id)) s.Jammed.Add(route.Id);
                if (owner.HasValue && game.IsRushHourRoad(route.Id)) s.Highways.Add(route.Id);
            }
            return s;
        }

        public readonly struct CardFlight
        {
            public readonly int Player;
            public readonly Resource Resource;
            public readonly int FaceUpSlot;
            public readonly float Delay;

            public CardFlight(int player, Resource resource, int faceUpSlot, float delay)
            {
                Player = player;
                Resource = resource;
                FaceUpSlot = faceUpSlot;
                Delay = delay;
            }
        }

        /// <summary>Preserve player/resource order, one use per changed face-up slot, and the original stagger.
        /// No UI, random draws or game commands are performed here.</summary>
        public static IEnumerable<CardFlight> PlanCardFlights(int[,] before, int[,] now, Resource?[] oldFaceUp, Resource?[] newFaceUp)
        {
            if (before == null || now == null || oldFaceUp == null || newFaceUp == null ||
                before.GetLength(0) != now.GetLength(0) || before.GetLength(1) != ResourceInfo.Count ||
                now.GetLength(1) != ResourceInfo.Count)
                yield break;

            var changedSlots = new List<int>();
            for (int i = 0; i < newFaceUp.Length && i < oldFaceUp.Length; i++)
                if (!Nullable.Equals(oldFaceUp[i], newFaceUp[i])) changedSlots.Add(i);

            int flight = 0;
            for (int p = 0; p < now.GetLength(0); p++)
            for (int c = 0; c < ResourceInfo.Count; c++)
            {
                int gained = now[p, c] - before[p, c];
                for (int k = 0; k < gained; k++)
                {
                    int slot = -1;
                    foreach (int index in changedSlots)
                    {
                        if (oldFaceUp[index] == (Resource)c)
                        {
                            slot = index;
                            break;
                        }
                    }
                    if (slot >= 0) changedSlots.Remove(slot);
                    yield return new CardFlight(p, (Resource)c, slot, flight++ * 0.15f);
                }
            }
        }
    }
}
