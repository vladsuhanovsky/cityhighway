namespace CityHighways.Presentation
{
    /// <summary>
    /// Conservative advisory conclusion for a manually collected frame benchmark suite.
    /// This status is never a hardware performance certification or an Edit Mode CI gate.
    /// </summary>
    public static class City3DPerformanceSuiteVerdict
    {
        public enum Status
        {
            NoData,
            Regression,
            Incomplete,
            UnverifiedLegacy,
            Pass
        }

        public static Status Evaluate(bool baselineValid, bool currentValid,
            City3DPerformanceDiagnostics.Report report)
        {
            if (!baselineValid || !currentValid || report.Matched == 0)
                return Status.NoData;
            if (report.Regressions > 0)
                return Status.Regression;
            if (report.Missing > 0 || report.Additional > 0 || report.Invalid > 0 ||
                report.Passed + report.Regressions + report.Invalid != report.Matched)
                return Status.Incomplete;
            if (report.LegacyMatches > 0)
                return Status.UnverifiedLegacy;
            return Status.Pass;
        }

        public static string Explain(Status status)
        {
            switch (status)
            {
                case Status.Pass:
                    return "PASS: all baseline scenarios matched, no P95 regressions, " +
                        "and recorded capture environments agree. " +
                        "Manual seed and camera consistency still require verification.";
                case Status.Regression:
                    return "REGRESSION: at least one matched scenario exceeded the " +
                        "allowed P95 frame-time increase.";
                case Status.Incomplete:
                    return "INCOMPLETE: scenario coverage, environment agreement or " +
                        "valid comparable measurements are missing.";
                case Status.UnverifiedLegacy:
                    return "UNVERIFIED: historical baseline comparisons lack " +
                        "automatically captured environment metadata.";
                default:
                    return "NO DATA: load valid baseline and current suites " +
                        "with at least one matched scenario.";
            }
        }
    }
}
