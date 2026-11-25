using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Presentation
{
    /// <summary>
    /// A two-phase local replay index update. The index is durably written
    /// before stale files are deleted. The former index remains available
    /// when writing fails. Neither planning nor verification touches disk.
    /// </summary>
    public sealed class ReplayIndexTransaction
    {
        public readonly List<Replays.Entry> Entries;
        public readonly List<string> RemovedIds;

        public ReplayIndexTransaction(IEnumerable<Replays.Entry> source, int keep)
        {
            var before = (source ?? Enumerable.Empty<Replays.Entry>()).ToList();
            Entries = ReplayRetention.Select(before, keep);
            RemovedIds = ReplayRetention.Removed(before, Entries);
        }

        public bool Contains(string id) => ReplayStorageIO.IsSafeId(id) &&
            Entries.Any(e => string.Equals(e.id, id, StringComparison.Ordinal));

        /// <summary>Removal callback is never invoked unless the index commits.</summary>
        public bool Commit(Func<List<Replays.Entry>, bool> persist,
            Action<IReadOnlyList<string>> deleteCommitted)
        {
            if (persist == null) throw new ArgumentNullException(nameof(persist));
            if (!persist(Entries)) return false;
            deleteCommitted?.Invoke(RemovedIds);
            return true;
        }
    }
}
