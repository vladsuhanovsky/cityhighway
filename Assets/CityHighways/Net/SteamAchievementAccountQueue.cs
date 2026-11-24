using System;
using System.Collections.Generic;
using System.Linq;

namespace CityHighways.Net
{
    /// <summary>
    /// In-memory Steam achievement upload queue owned by exactly one local
    /// SteamID. Account changes atomically discard uncommitted work and known
    /// API-name errors. No Steamworks.NET dependency: can be tested in Editor.
    /// </summary>
    public sealed class SteamAchievementAccountQueue
    {
        readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _staged = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _unknown = new HashSet<string>(StringComparer.Ordinal);

        public ulong AccountId { get; private set; }
        public int PendingCount => _pending.Count;
        public int StagedCount => _staged.Count;
        public int UnknownCount => _unknown.Count;

        /// <summary>
        /// Bind to the currently authenticated local SteamID, or zero if Steam
        /// is unavailable. Returns true if the binding changed.
        /// </summary>
        public bool ObserveAccount(ulong currentSteamId)
        {
            if (AccountId == currentSteamId) return false;
            Reset();
            AccountId = currentSteamId;
            return true;
        }

        public bool BelongsTo(ulong steamId) =>
            steamId != 0UL && steamId == AccountId;

        public void Reset()
        {
            AccountId = 0UL;
            _pending.Clear();
            _staged.Clear();
            _unknown.Clear();
        }

        /// <summary>
        /// Accept only owned, nonblank IDs. The publisher/bridge must also
        /// filter IDs against the game's canonical achievement catalog.
        /// </summary>
        public int QueueForAccount(ulong steamId, IEnumerable<string> ids)
        {
            if (!BelongsTo(steamId) || ids == null) return 0;
            int added = 0;
            foreach (string id in ids)
                if (!string.IsNullOrWhiteSpace(id) && _pending.Add(id))
                    added++;
            return added;
        }

        /// <summary>
        /// Returns a snapshot to safely enumerate while updates remove items.
        /// </summary>
        public string[] PendingSnapshot() =>
            _pending.OrderBy(id => id, StringComparer.Ordinal).ToArray();

        public bool ContainsPending(string id) =>
            !string.IsNullOrEmpty(id) && _pending.Contains(id);

        public bool ContainsStaged(string id) =>
            !string.IsNullOrEmpty(id) && _staged.Contains(id);

        public bool Stage(string id) =>
            ContainsPending(id) && _staged.Add(id);

        /// <summary>
        /// Already persisted in Steam: forget it only when no unconfirmed
        /// StoreStats attempt has staged it.
        /// </summary>
        public bool ForgetPersisted(string id) =>
            !ContainsStaged(id) && !string.IsNullOrEmpty(id) &&
            _pending.Remove(id);

        /// <summary>
        /// A failed StoreStats leaves staged work for the next retry.
        /// Success consumes only the staged IDs, never unrelated new IDs.
        /// </summary>
        public bool CompleteStore(ulong steamId, bool success)
        {
            if (!BelongsTo(steamId)) return false;
            if (!success) return false;
            foreach (string id in _staged)
                _pending.Remove(id);
            _staged.Clear();
            return true;
        }

        /// <summary>Log an unavailable API name once per account session.</summary>
        public bool FirstUnknownId(string id) =>
            !string.IsNullOrWhiteSpace(id) && _unknown.Add(id);
    }
}
