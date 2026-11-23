namespace CityHighways.Presentation
{
    /// <summary>
    /// Advisory check against an explicitly loaded baseline before adding a new capture.
    /// Exact scenario identity is supplied by the tester; the generator seed is not verified.
    /// </summary>
    public static class City3DPerformanceBaselinePreflight
    {
        public enum Status
        {
            NoBaseline,
            MissingScenario,
            IncompatibleEnvironment,
            UnknownLegacyEnvironment,
            Ready
        }

        public struct Result
        {
            public Status State;
            public string Explanation;
            public bool RequiresAcknowledgement;
        }

        public static Result Check(City3DPerformanceSuite baseline,
            string city, int quality, string scenario,
            City3DPerformanceEnvironment environment)
        {
            if (baseline == null || !baseline.IsValid(out _))
                return Create(Status.NoBaseline,
                    "No valid baseline suite loaded; this is a new or unverified capture.",
                    false);

            City3DPerformanceBaseline matched = null;
            foreach (var item in baseline.entries)
                if (item.city == city && item.quality == quality &&
                    item.scenario == scenario)
                {
                    matched = item;
                    break;
                }
            if (matched == null)
                return Create(Status.MissingScenario,
                    "No baseline scenario matches this city, graphics quality and exact label. " +
                    "It will appear as Additional rather than Pass.", true);
            var compatibility = City3DPerformanceEnvironment.Compare(
                matched.environment, environment);
            if (compatibility == City3DPerformanceEnvironment.Compatibility.Different)
                return Create(Status.IncompatibleEnvironment,
                    "Baseline capture environment differs from the current runtime " +
                    "(Unity, platform, GPU, resolution, VSync or FPS cap).", true);
            if (compatibility == City3DPerformanceEnvironment.Compatibility.Incomplete ||
                compatibility == City3DPerformanceEnvironment.Compatibility.BothLegacy)
                return Create(Status.UnknownLegacyEnvironment,
                    "Baseline or current capture environment is missing or invalid. " +
                    "The comparison cannot be verified.", true);
            return Create(Status.Ready,
                "Exact baseline label and recorded capture conditions match. " +
                "Generator seed, camera and scene workload still need manual verification.",
                false);
        }

        static Result Create(Status state, string explanation, bool acknowledge) =>
            new Result
            {
                State = state,
                Explanation = explanation,
                RequiresAcknowledgement = acknowledge
            };
    }
}
