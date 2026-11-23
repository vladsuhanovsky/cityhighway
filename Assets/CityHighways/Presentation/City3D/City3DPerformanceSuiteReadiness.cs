using System;
using System.Collections.Generic;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Read-only preparation checklist for manually gathered CURRENT benchmark captures.
    /// Unlike the performance verdict, this checks collection completeness, not frame speed.
    /// </summary>
    public static class City3DPerformanceSuiteReadiness
    {
        public struct Item
        {
            public string City;
            public int Quality;
            public string Scenario;
            public bool Present;
            public bool EnvironmentCompatible;
            public string Reason;
        }

        public struct Report
        {
            public bool HasBaseline;
            public int Expected;
            public int Ready;
            public int Missing;
            public int Incompatible;
            public int Extra;
            public int UnverifiedLabels;
            public bool Complete;
            public Item[] Items;
            public string Summary;
        }

        static bool Same(City3DPerformanceBaseline a, City3DPerformanceBaseline b) =>
            string.Equals(a.city, b.city, StringComparison.Ordinal) &&
            a.quality == b.quality &&
            string.Equals(a.scenario, b.scenario, StringComparison.Ordinal);

        public static Report Check(City3DPerformanceSuite baseline,
            City3DPerformanceSuite current)
        {
            var report = new Report { Items = new Item[0] };
            if (baseline == null || !baseline.IsValid(out _))
            {
                report.Summary = "Load a valid baseline suite to see the collection checklist.";
                return report;
            }

            report.HasBaseline = true;
            report.Expected = baseline.entries.Count;
            bool currentValid = current != null && current.IsValid(out _);
            var items = new List<Item>();
            foreach (var original in baseline.entries)
            {
                City3DPerformanceBaseline captured = null;
                if (currentValid)
                    foreach (var entry in current.entries)
                        if (Same(original, entry)) { captured = entry; break; }

                var item = new Item
                {
                    City = original.city,
                    Quality = original.quality,
                    Scenario = original.scenario,
                    Present = captured != null
                };
                if (captured == null)
                {
                    item.Reason = "Capture missing from CURRENT suite.";
                    report.Missing++;
                }
                else
                {
                    var compatibility = City3DPerformanceEnvironment.Compare(
                        original.environment, captured.environment);
                    item.EnvironmentCompatible =
                        compatibility == City3DPerformanceEnvironment.Compatibility.Match;
                    if (!item.EnvironmentCompatible)
                    {
                        item.Reason = compatibility ==
                            City3DPerformanceEnvironment.Compatibility.BothLegacy
                            ? "Both captures have unknown legacy environments."
                            : compatibility ==
                                City3DPerformanceEnvironment.Compatibility.Incomplete
                                ? "Environment metadata is incomplete."
                                : "Recorded capture environments differ.";
                        report.Incompatible++;
                    }
                    else
                    {
                        report.Ready++;
                        item.Reason = "Captured with matching observed environment.";
                    }
                }
                if (City3DPerformanceScenarioPreflight.Check(original.scenario,
                    original.environment).HasWarnings)
                    report.UnverifiedLabels++;
                items.Add(item);
            }
            if (currentValid)
                foreach (var captured in current.entries)
                {
                    bool found = false;
                    foreach (var original in baseline.entries)
                        if (Same(original, captured)) { found = true; break; }
                    if (!found) report.Extra++;
                }
            report.Items = items.ToArray();
            report.Complete = report.Missing == 0 && report.Incompatible == 0 &&
                report.Extra == 0 && report.Ready == report.Expected;
            report.Summary = report.Ready + "/" + report.Expected +
                " matching captures; " + report.Missing + " missing, " +
                report.Incompatible + " incomparable, " + report.Extra +
                " extra; " + report.UnverifiedLabels +
                " baseline labels need manual inspection.";
            return report;
        }
    }
}
