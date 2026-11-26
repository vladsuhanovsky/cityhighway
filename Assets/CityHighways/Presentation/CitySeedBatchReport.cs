using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CityHighways.Presentation
{
    [Serializable]
    public sealed class CitySeedBatchRow
    {
        public int requestedSeed, actualSeed, warnings, locations, roads, hubs;
        public bool generated, passed;
        public long milliseconds;
        public string cityName, reportPath;
        public string[] issues, warningCodes;
    }

    [Serializable]
    public sealed class CitySeedBatchReport
    {
        public int schema = 1;
        public int firstSeed, requestedCount;
        public bool include3D;
        public string utc, unityVersion;
        public List<CitySeedBatchRow> rows = new List<CitySeedBatchRow>();
        public int Completed => rows.Count;
        public int Failed => rows.Count(r => !r.passed);
        public int Warned => rows.Count(r => r.warnings > 0);
        public int Fallback => rows.Count(r => r.generated && r.requestedSeed != r.actualSeed);
        public bool Complete => Completed == requestedCount;

        public static CitySeedBatchReport Begin(int first, int count, bool include3D)
        {
            if (!CitySeedBatch.IsValid(first, count)) throw new ArgumentOutOfRangeException(nameof(count));
            return new CitySeedBatchReport { firstSeed = first, requestedCount = count, include3D = include3D,
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        }

        public void Add(CitySeedReport report, string reportPath)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (Complete || report.requestedSeed != (long)firstSeed + Completed)
                throw new InvalidOperationException("Unexpected seed or completed batch.");
            var warnings = report.layout3D?.warnings ?? Array.Empty<City3DLayoutIssue>();
            rows.Add(new CitySeedBatchRow
            {
                requestedSeed = report.requestedSeed, actualSeed = report.actualSeed, generated = report.generated,
                passed = report.Passed, cityName = report.cityName, milliseconds = report.milliseconds,
                locations = report.locations?.Length ?? 0, roads = report.roads?.Length ?? 0,
                hubs = report.hubs?.Length ?? 0, warnings = warnings.Length, reportPath = reportPath,
                issues = (report.issues ?? Array.Empty<string>()).ToArray(),
                warningCodes = warnings.Select(w => w.code).Distinct().OrderBy(w => w, StringComparer.Ordinal).ToArray()
            });
        }

        public string Write(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "city-batch-" + firstSeed + "-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(this, true), new UTF8Encoding(false));
            return path;
        }
    }
}
