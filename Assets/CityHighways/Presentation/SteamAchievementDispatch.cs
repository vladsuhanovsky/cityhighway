using System;
using CityHighways.Net;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Owns only local dispatch deduplication. The Steam platform remains
    /// responsible for RequestCurrentStats, SetAchievement and StoreStats.
    /// No Steamworks.NET dependency; exercised by Editor tests with fakes.
    /// </summary>
    public sealed class SteamAchievementDispatch
    {
        /// <summary>Periodically requeue owned IDs after reconnect/store failures.</summary>
        public const float ReconcileSeconds = 120f;

        ISteamAchievements _lastPublisher;
        PlayerProfile _lastProfile;
        string _lastSteamId;
        string _lastSignature;
        float _nextReconcile;

        public void Reset()
        {
            _lastPublisher = null;
            _lastProfile = null;
            _lastSteamId = null;
            _lastSignature = null;
            _nextReconcile = 0f;
        }

        /// <summary>
        /// Queues only IDs earned by the active profile matching the current
        /// Steam account. No cached signature is committed until queueing
        /// succeeds, so exceptions remain retriable.
        ///
        /// The optional platform's queue is idempotent; periodic resubmission
        /// ensures that a missing/failed callback cannot stall sync forever.
        /// A missing or mismatched identity invalidates prior dispatch state.
        /// </summary>
        public bool Reconcile(ISteamAchievements publisher, PlayerProfile profile,
            string steamId, float unscaledTime)
        {
            if (publisher == null ||
                !SteamAchievementSync.ShouldPublish(profile, steamId))
            {
                Reset();
                return false;
            }

            var ids = SteamAchievementSync.EligibleIds(profile, steamId);
            if (ids.Count == 0)
            {
                Reset();
                return false;
            }

            string signature = string.Join("|", ids);
            bool same = ReferenceEquals(_lastPublisher, publisher) &&
                        ReferenceEquals(_lastProfile, profile) &&
                        string.Equals(_lastSteamId, steamId, StringComparison.Ordinal) &&
                        string.Equals(_lastSignature, signature, StringComparison.Ordinal);
            if (same && unscaledTime >= 0f && unscaledTime < _nextReconcile)
                return false;

            // Important: a failed Queue call must not be recorded as
            // delivered; the next Tick can safely try again.
            // Native Steam implementation also validates the source
            // profile account at queue time. This closes the identity
            // race between reading the profile and sending achievements.
            if (publisher is IAccountBoundSteamAchievements accountBound)
            {
                if (!accountBound.TryQueueUnlockedAchievementsForAccount(steamId, ids))
                {
                    Reset(); // A rejected account handoff was never queued.
                    return false;
                }
            }
            else
                publisher.QueueUnlockedAchievements(ids);
            _lastPublisher = publisher;
            _lastProfile = profile;
            _lastSteamId = steamId;
            _lastSignature = signature;
            _nextReconcile = unscaledTime + ReconcileSeconds;
            return true;
        }
    }
}
