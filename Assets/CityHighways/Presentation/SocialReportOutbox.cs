using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Disk-backed queue of social game reports. Contains NO Steam auth ticket;
    /// the transport obtains a fresh one at upload time. Entries are scoped to
    /// a single Steam account and cannot be drained by a different account.
    /// </summary>
    public sealed class SocialReportOutbox
    {
        public const int MaxEntries = 24;
        public const int MaxPayloadBytes = 740 * 1024;
        public const long MaxFileBytes = (long)MaxEntries * MaxPayloadBytes + 256 * 1024;

        [Serializable]
        public sealed class Entry
        {
            public string ownerSteamId;
            public string key;
            public string requestJson;
            public long createdUtcTicks;
            public long nextAttemptUtcTicks;
            public int attempts;
        }

        [Serializable]
        sealed class State
        {
            public List<Entry> entries = new List<Entry>();
        }

        readonly string _path;
        readonly List<Entry> _entries = new List<Entry>();

        public int Count => _entries.Count;
        public IReadOnlyList<Entry> Entries => _entries;

        public SocialReportOutbox(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path required", nameof(path));
            _path = path;
            Load();
        }

        public static bool ValidOwner(string owner)
        {
            if (string.IsNullOrEmpty(owner) || owner.Length < 5 || owner.Length > 20) return false;
            foreach (char c in owner)
                if (c < '0' || c > '9') return false;
            return true;
        }

        public bool Enqueue(string owner, string key, string requestJson, long nowTicks)
        {
            if (!ValidOwner(owner) || string.IsNullOrEmpty(key) || key.Length > 200 ||
                string.IsNullOrEmpty(requestJson) ||
                Encoding.UTF8.GetByteCount(requestJson) > MaxPayloadBytes || nowTicks < 0)
                return false;

            if (_entries.Any(e => e.ownerSteamId == owner && e.key == key))
                return true; // Same report queued twice before the HTTP response.
            if (_entries.Count >= MaxEntries) return false;

            var entry = new Entry
            {
                ownerSteamId = owner,
                key = key,
                requestJson = requestJson,
                createdUtcTicks = nowTicks,
                nextAttemptUtcTicks = nowTicks
            };
            _entries.Add(entry);
            if (Save()) return true;
            // Never pretend a report was queued when the disk write failed.
            _entries.Remove(entry);
            return false;
        }

        /// <summary>Only sends a due report owned by the currently signed-in Steam account.</summary>
        public Entry Next(string owner, long nowTicks)
        {
            if (!ValidOwner(owner)) return null;
            return _entries.Where(e => e.ownerSteamId == owner && e.nextAttemptUtcTicks <= nowTicks)
                .OrderBy(e => e.createdUtcTicks).FirstOrDefault();
        }

        public bool Complete(Entry entry)
        {
            if (entry == null) return false;
            int index = _entries.IndexOf(entry);
            if (index < 0) return false;
            _entries.RemoveAt(index);
            if (Save()) return true;
            // Restore the pending report: an acknowledged response must not
            // silently drop the only in-memory copy after a failed write.
            _entries.Insert(index, entry);
            return false;
        }

        public bool Reschedule(Entry entry, long nowTicks, long httpStatus)
        {
            if (entry == null || !_entries.Contains(entry)) return false;
            if (nowTicks < DateTime.MinValue.Ticks || nowTicks > DateTime.MaxValue.Ticks)
                return false;
            int previousAttempts = entry.attempts;
            long previousNextAttempt = entry.nextAttemptUtcTicks;
            entry.attempts = Math.Min(1000, Math.Max(0, entry.attempts) + 1);
            long delay = RetryDelaySeconds(entry.attempts, httpStatus);
            entry.nextAttemptUtcTicks = nowTicks >= DateTime.MaxValue.Ticks - delay * TimeSpan.TicksPerSecond
                ? DateTime.MaxValue.Ticks
                : nowTicks + delay * TimeSpan.TicksPerSecond;
            if (Save()) return true;
            entry.attempts = previousAttempts;
            entry.nextAttemptUtcTicks = previousNextAttempt;
            return false;
        }

        /// <summary>Malformed requests need human/patch intervention, not a retry every frame.</summary>
        /// <summary>
        /// Club targets roll over on Monday 00:00 UTC. Retried reports from an
        /// earlier week may still update personal records but must never
        /// contribute to a later week's club goal.
        /// </summary>
        public static bool SameUtcWeek(long startedTicks, long nowTicks)
        {
            if (startedTicks < DateTime.MinValue.Ticks || startedTicks > DateTime.MaxValue.Ticks ||
                nowTicks < DateTime.MinValue.Ticks || nowTicks > DateTime.MaxValue.Ticks)
                return false;
            var started = new DateTime(startedTicks, DateTimeKind.Utc).Date;
            var now = new DateTime(nowTicks, DateTimeKind.Utc).Date;
            int startOffset = ((int)started.DayOfWeek + 6) % 7;
            int nowOffset = ((int)now.DayOfWeek + 6) % 7;
            return started.AddDays(-startOffset) == now.AddDays(-nowOffset);
        }

        public static long RetryDelaySeconds(int attempt, long httpStatus)
        {
            if (httpStatus == 400 || httpStatus == 403 || httpStatus == 405 ||
                httpStatus == 413 || httpStatus == 422)
                return 3600; // Keep the report; configuration or client may be fixed later.
            if (httpStatus == 401 || httpStatus == 404)
                return 300; // Steam auth / profile syncing may recover.
            if (httpStatus == 429)
                return 900;
            int shift = Math.Min(6, Math.Max(0, attempt - 1));
            return Math.Min(900, 15L << shift);
        }

        void Load()
        {
            foreach (string candidate in new[] { _path, _path + ".bak" })
            {
                try
                {
                    if (!File.Exists(candidate)) continue;
                    if (new FileInfo(candidate).Length > MaxFileBytes)
                    {
                        Debug.LogWarning("Social report queue is larger than its disk limit.");
                        continue;
                    }
                    var state = JsonUtility.FromJson<State>(File.ReadAllText(candidate));
                    if (state?.entries == null) continue;
                    _entries.Clear();
                    foreach (var entry in state.entries)
                    {
                        if (entry != null && ValidOwner(entry.ownerSteamId) &&
                            !string.IsNullOrEmpty(entry.key) && entry.key.Length <= 200 &&
                            !string.IsNullOrEmpty(entry.requestJson) &&
                            Encoding.UTF8.GetByteCount(entry.requestJson) <= MaxPayloadBytes &&
                            !_entries.Any(e => e.key == entry.key && e.ownerSteamId == entry.ownerSteamId))
                            _entries.Add(entry);
                        if (_entries.Count >= MaxEntries) break;
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Social report queue read failed: " + ex.Message);
                }
            }
        }

        bool Save()
        {
            string temp = _path + ".tmp";
            try
            {
                string folder = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(temp, JsonUtility.ToJson(new State { entries = _entries }));
                if (File.Exists(_path))
                    File.Replace(temp, _path, _path + ".bak");
                else
                    File.Move(temp, _path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Social report queue write failed: " + ex.Message);
                return false;
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch (Exception) { /* Temporary file can be cleaned on next save. */ }
            }
        }
    }
}
