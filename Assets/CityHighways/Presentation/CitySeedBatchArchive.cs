using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Read-only import of bounded diagnostic summaries. Embedded report paths
    /// are informational and are never opened or used as destinations.</summary>
    public static class CitySeedBatchArchive
    {
        public const int MaxBytes = 4 * 1024 * 1024;
        public const int MaxScan = 300;
        public const int MaxVisible = 50;

        public static CitySeedBatchReport Read(string path)
        {
            string json = ReplayStorageIO.ReadBoundedUtf8(path, MaxBytes);
            var batch = new CitySeedBatchReport { schema = 0, rows = null };
            try { JsonUtility.FromJsonOverwrite(json, batch); }
            catch (ArgumentException ex) { throw new InvalidDataException("Invalid batch JSON.", ex); }
            Validate(batch);
            return batch;
        }

        public static void Validate(CitySeedBatchReport batch)
        {
            if (batch == null || batch.schema != 1 || !CitySeedBatch.IsValid(batch.firstSeed, batch.requestedCount) ||
                batch.rows == null || batch.rows.Count > batch.requestedCount ||
                !DateTime.TryParseExact(batch.utc, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _) ||
                string.IsNullOrWhiteSpace(batch.unityVersion))
                throw new InvalidDataException("Invalid or unsupported batch summary.");
            for (int i = 0; i < batch.rows.Count; i++)
            {
                var r = batch.rows[i];
                if (r == null || r.requestedSeed != (long)batch.firstSeed + i ||
                    r.milliseconds < 0 || r.locations < 0 || r.roads < 0 || r.hubs < 0 || r.warnings < 0 || r.warnings > 1000000 ||
                    r.issues == null || r.warningCodes == null || r.issues.Any(string.IsNullOrWhiteSpace) ||
                    r.warningCodes.Any(string.IsNullOrWhiteSpace) ||
                    r.warningCodes.Distinct(StringComparer.Ordinal).Count() != r.warningCodes.Length ||
                    r.warningCodes.Length > r.warnings || (r.warnings > 0 && r.warningCodes.Length == 0) ||
                    (!batch.include3D && r.warnings != 0) ||
                    r.passed != (r.generated && r.issues.Length == 0))
                    throw new InvalidDataException("Invalid batch row at index " + i + ".");
            }
        }

        public static string[] List(string directory)
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            // Bounded non-recursive scan; newest among scanned entries, not a full directory index.
            return Directory.EnumerateFiles(directory, "city-batch-*.json", SearchOption.TopDirectoryOnly)
                .Take(MaxScan)
                .Select(p => new FileInfo(p))
                .Where(f => f.Length <= MaxBytes && (f.Attributes & FileAttributes.ReparsePoint) == 0)
                .OrderByDescending(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal)
                .Take(MaxVisible).Select(f => f.FullName).ToArray();
        }
    }

    public sealed class CitySeedBatchComparison
    {
        public int Shared, OnlyBaseline, OnlyCurrent, NewFailures, ResolvedFailures,
            NewWarnings, ResolvedWarnings, ActualSeedChanged, SizeChanged;
        public int WarningDelta, GeneratedInBoth;

        public static CitySeedBatchComparison Compare(CitySeedBatchReport baseline, CitySeedBatchReport current)
        {
            CitySeedBatchArchive.Validate(baseline);
            CitySeedBatchArchive.Validate(current);
            if (baseline.include3D != current.include3D)
                throw new ArgumentException("Compare batches with the same diagnostic mode.");
            var before = baseline.rows.ToDictionary(r => r.requestedSeed);
            var result = new CitySeedBatchComparison();
            foreach (var after in current.rows)
            {
                if (!before.TryGetValue(after.requestedSeed, out var old))
                { result.OnlyCurrent++; continue; }
                result.Shared++;
                if (old.passed && !after.passed) result.NewFailures++;
                if (!old.passed && after.passed) result.ResolvedFailures++;
                if (old.generated && after.generated && old.passed && after.passed)
                {
                    result.GeneratedInBoth++;
                    if (old.warnings == 0 && after.warnings > 0) result.NewWarnings++;
                    if (old.warnings > 0 && after.warnings == 0) result.ResolvedWarnings++;
                    if (old.actualSeed != after.actualSeed) result.ActualSeedChanged++;
                    if (old.locations != after.locations || old.roads != after.roads || old.hubs != after.hubs)
                        result.SizeChanged++;
                    result.WarningDelta += after.warnings - old.warnings;
                }
            }
            result.OnlyBaseline = baseline.Completed - result.Shared;
            return result;
        }
    }
}
