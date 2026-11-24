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
        static readonly SteamAchievementDispatch Dispatch = new SteamAchievementDispatch();

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
                if (steam == null || publisher == null || !steam.Available)
                {
                    Dispatch.Reset(); // A fresh session must reconcile owned IDs.
                    return;
                }

                // A different Steam account or a local/offline profile must
                // never inherit the previously cached achievement signature.
                Dispatch.Reconcile(publisher, Profiles.Active, steam.SteamId,
                    Time.unscaledTime);
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
            Dispatch.Reset();
        }
    }
}
