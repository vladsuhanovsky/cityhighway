using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CityHighways.Presentation
{
    public static class CampaignStorage
    {
        [Serializable]
        sealed class Progress { public int version = 1; public int completed; }

        public static string Owner(PlayerProfile profile, bool create = false)
        {
            if (profile == null) return null;
            if (profile.IsSteam) return SocialReportOutbox.ValidOwner(profile.steamId) ? "steam:" + profile.steamId : null;
            if (Guid.TryParseExact(profile.campaignId, "N", out _)) return "local:" + profile.campaignId;
            if (!create) return null;
            string old = profile.campaignId;
            profile.campaignId = Guid.NewGuid().ToString("N");
            if (Profiles.TrySaveLocal()) return "local:" + profile.campaignId;
            profile.campaignId = old;
            return null;
        }

        public static bool ValidOwner(string owner) =>
            owner != null && ((owner.StartsWith("local:", StringComparison.Ordinal) && Guid.TryParseExact(owner.Substring(6), "N", out _)) ||
            (owner.StartsWith("steam:", StringComparison.Ordinal) && SocialReportOutbox.ValidOwner(owner.Substring(6))));

        static string Folder(string owner)
        {
            if (!ValidOwner(owner)) throw new ArgumentException("Invalid campaign owner.");
            using (var sha = SHA256.Create())
                return Path.Combine(Application.persistentDataPath, "campaign", BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(owner))).Replace("-", "").ToLowerInvariant());
        }

        public static string CheckpointPath(string owner) => Path.Combine(Folder(owner), "checkpoint.json");

        static string ProgressPath(string owner) => Path.Combine(Folder(owner), "progress.json");

        public static bool TryRead(string owner, out int completed)
        {
            completed = 0;
            if (!ValidOwner(owner)) return false;
            try
            {
                string path = ProgressPath(owner);
                if (!File.Exists(path)) return true;
                var progress = JsonUtility.FromJson<Progress>(File.ReadAllText(path));
                if (progress == null || progress.version != 1 || progress.completed < 0 || progress.completed > CampaignCatalog.All.Count) return false;
                completed = progress.completed;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("Campaign progress: " + e.Message); return false; }
        }

        public static bool CanEnter(GameSetup setup) => setup != null && ValidOwner(setup.campaignOwner) &&
            setup.campaignOwner == Owner(Profiles.Active) && TryRead(setup.campaignOwner, out int completed) &&
            CampaignCatalog.Unlocked(setup.campaignMission, completed) &&
            CampaignCatalog.Find(setup.campaignMission)?.Matches(setup) == true;

        public static bool TryComplete(string owner, string id)
        {
            if (!ValidOwner(owner) || !TryRead(owner, out int completed) || !CampaignCatalog.Unlocked(id, completed)) return false;
            int next = CampaignCatalog.Advance(id, completed);
            if (next == completed) return CampaignCatalog.Index(id) < completed;
            try
            {
                ReplayStorageIO.WriteAtomic(ProgressPath(owner), JsonUtility.ToJson(new Progress { completed = next }));
                return true;
            }
            catch (Exception e) { Debug.LogWarning("Campaign progress: " + e.Message); return false; }
        }
    }
}
