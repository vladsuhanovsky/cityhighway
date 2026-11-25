using System;
using System.IO;
using System.Linq;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Bounded Classic/campaign save I/O. Backup is explicit recovery
    /// material, never silently loaded instead of the primary file.</summary>
    public static class SaveFileStorage
    {
        public const int MaxSaveBytes = 16 * 1024 * 1024;
        public static string BackupPath(string path) => path + ".bak";

        public static SaveFile Read(string path, string owner)
        {
            if (!File.Exists(path)) return null;
            return Parse(ReplayStorageIO.ReadBoundedUtf8(path, MaxSaveBytes), owner);
        }

        static SaveFile Parse(string json, string owner)
        {
            var file = JsonUtility.FromJson<SaveFile>(json);
            if (file == null || file.game == null || file.setup == null || file.setup.players == null ||
                file.game.players == null || file.game.players.Length < 2 ||
                file.setup.players.Count != file.game.players.Length ||
                file.setup.players.Any(p => p == null) || file.game.players.Any(p => p == null))
                return null;
            if (owner == null ? file.setup.IsCampaign :
                !CampaignStorage.ValidOwner(owner) || !file.setup.IsCampaign ||
                file.setup.campaignOwner != owner || CampaignCatalog.Find(file.setup.campaignMission) == null)
                return null;
            return file;
        }

        public static void Write(string path, SaveFile file)
        {
            if (file?.setup == null || file.game == null) throw new ArgumentException("Missing save data.");
            string owner = file.setup.IsCampaign ? file.setup.campaignOwner : null;
            string json = JsonUtility.ToJson(file);
            ReplayStorageIO.RequireUtf8Size(json, MaxSaveBytes);
            if (Parse(json, owner) == null) throw new InvalidDataException("Invalid save scope.");

            // Only rotate a readable, scope-matching supported snapshot into the backup.
            // A damaged or newer primary must not replace an existing useful backup.
            string previous = null;
            if (File.Exists(path))
            {
                try
                {
                    string raw = ReplayStorageIO.ReadBoundedUtf8(path, MaxSaveBytes);
                    var candidate = Parse(raw, owner);
                    if (candidate != null && candidate.game.version >= GameSaveData.OldestSupportedVersion &&
                        candidate.game.version <= GameSaveData.CurrentVersion)
                        previous = raw;
                }
                catch (ArgumentException) { /* Invalid JSON or UTF-8. */ }
                catch (InvalidDataException) { /* Oversized file. */ }
            }
            if (previous != null) ReplayStorageIO.WriteAtomic(BackupPath(path), previous);
            ReplayStorageIO.WriteAtomic(path, json);
        }

        /// <summary>Explicit opt-in recovery. Validate the backup against the
        /// current map before changing anything; archive original bytes first.
        /// Returns the archive path, or null when no primary existed.</summary>
        public static string RestoreBackup(string path, string owner)
        {
            string raw = ReplayStorageIO.ReadBoundedUtf8(BackupPath(path), MaxSaveBytes);
            var file = Parse(raw, owner);
            if (file == null) throw new InvalidDataException("Backup has an invalid save scope or shape.");
            // Do not publish any replay results or touch profiles: this is a local model load.
            Game.Load(CityHighwaysApp.LoadMap(file.setup.mapResourcePath), file.game);
            string archive = null;
            if (File.Exists(path))
            {
                archive = path + ".before-restore-" + Guid.NewGuid().ToString("N");
                File.Copy(path, archive, false);
            }
            ReplayStorageIO.WriteAtomic(path, raw);
            return archive;
        }

        /// <summary>Explicit discard or normal end-of-game cleanup, never on load failure.</summary>
        public static void Delete(string path)
        {
            string backup = BackupPath(path);
            if (File.Exists(backup)) File.Delete(backup);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
