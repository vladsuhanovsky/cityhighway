using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Net;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Bridges existing profile achievements to optional Steam publishing.
    /// No duplicate achievement predicates and no changes to profile rewards.
    /// Only an authenticated local Steam profile can publish achievements.
    /// </summary>
    public static class SteamAchievementSync
    {
        static readonly HashSet<string> KnownIds = new HashSet<string>(
            Achievements.All.Select(a => a.Id), StringComparer.Ordinal);

        static float _nextRefresh;
        static string _lastSteamId;
        static string _lastSignature;
        static ISteamAchievements _lastPublisher;

        public static List<string> EligibleIds(PlayerProfile profile, string localSteamId)
        {
            if (profile == null || string.IsNullOrEmpty(localSteamId) ||
                !string.Equals(profile.steamId, localSteamId, StringComparison.Ordinal) ||
                profile.achievements == null)
                return new List<string>();

            return profile.achievements
                .Where(id => !string.IsNullOrEmpty(id) && KnownIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        public static bool ShouldPublish(PlayerProfile profile, string steamId) =>
            profile != null && !string.IsNullOrEmpty(steamId) &&
            string.Equals(profile.steamId, steamId, StringComparison.Ordinal) &&
            profile.achievements != null;

        /// <summary>
        /// Reconcile on app startup, after a completed game or cloud refresh.
        /// Limited to once every 3 seconds; actual Steam callbacks and upload
        /// retries are managed by the optional Steam assembly.
        /// </summary>
        public static void Tick()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 3f;

            try
            {
                var steam = NetPlatforms.Steam;
                var publisher = NetPlatforms.SteamAchievements;
                if (steam == null || publisher == null || !steam.Available) return;
                var profile = Profiles.Active;
                var steamId = steam.SteamId;
                if (!ShouldPublish(profile, steamId)) return;

                var ids = EligibleIds(profile, steamId);
                string signature = string.Join("|", ids);
                if (ReferenceEquals(_lastPublisher, publisher) &&
                    _lastSteamId == steamId && _lastSignature == signature)
                    return;

                publisher.QueueUnlockedAchievements(ids);
                _lastPublisher = publisher;
                _lastSteamId = steamId;
                _lastSignature = signature;
            }
            catch (Exception e)
            {
                // Steam is optional. A platform/API failure must not interrupt
                // gameplay or local achievement rewards; retry at a slow pace.
                _nextRefresh = Time.unscaledTime + 30f;
                Debug.LogWarning("City Highways: Steam achievement sync failed. " + e.Message);
            }
        }

        /// <summary>For new sessions/tests: force a full owned-ID reconciliation.</summary>
        public static void Reset()
        {
            _nextRefresh = 0f;
            _lastSteamId = null;
            _lastSignature = null;
            _lastPublisher = null;
        }
    }
}
