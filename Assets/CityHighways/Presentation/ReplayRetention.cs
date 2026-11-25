using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Pure, deterministic retention policy for local replay metadata.
    /// Preserve the highest score per author/map/mode while making room
    /// for recently saved games. Never delete files until the new index
    /// has been committed to disk.
    /// </summary>
    public static class ReplayRetention
    {
        public static List<Replays.Entry> Select(IEnumerable<Replays.Entry> source, int keep)
        {
            if (source == null || keep <= 0) return new List<Replays.Entry>();
            var entries = source.Where(e => e != null && ReplayStorageIO.IsSafeId(e.id))
                .GroupBy(e => e.id, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(e => e.date ?? "", StringComparer.Ordinal)
                    .ThenByDescending(e => e.authorScore).First()).ToList();
            if (entries.Count <= keep) return entries;

            // A winning score from each author/mode/map is protected, but
            // the protection itself must not exceed the finite quota.
            var best = entries
                .GroupBy(e => (e.mode ?? "", e.mapPath ?? "", (e.author ?? "").ToLowerInvariant(), e.mode == "coop" ? (int)CityHighways.Core.CooperativeDifficulty.Normalize(e.coopDifficulty) : 0))
                .Select(g => g.OrderByDescending(e => e.authorScore)
                    .ThenByDescending(e => e.date ?? "", StringComparer.Ordinal)
                    .ThenBy(e => e.id, StringComparer.Ordinal).First())
                .OrderByDescending(e => e.date ?? "", StringComparer.Ordinal)
                .ThenBy(e => e.id, StringComparer.Ordinal)
                .Take(keep).ToList();

            var selected = new HashSet<string>(best.Select(e => e.id), StringComparer.Ordinal);
            foreach (var entry in entries.OrderByDescending(e => e.date ?? "", StringComparer.Ordinal)
                .ThenByDescending(e => e.authorScore).ThenBy(e => e.id, StringComparer.Ordinal))
            {
                if (selected.Count >= keep) break;
                selected.Add(entry.id);
            }
            return entries.Where(e => selected.Contains(e.id)).ToList();
        }

        public static List<string> Removed(IEnumerable<Replays.Entry> before,
            IEnumerable<Replays.Entry> after)
        {
            var kept = new HashSet<string>((after ?? Enumerable.Empty<Replays.Entry>())
                .Where(e => e != null).Select(e => e.id), StringComparer.Ordinal);
            return (before ?? Enumerable.Empty<Replays.Entry>())
                .Where(e => e != null && ReplayStorageIO.IsSafeId(e.id) && !kept.Contains(e.id))
                .Select(e => e.id).Distinct(StringComparer.Ordinal).ToList();
        }
    }
}
