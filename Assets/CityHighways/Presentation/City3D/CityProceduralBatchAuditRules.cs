using System;

namespace CityHighways.Presentation
{
    /// <summary>Pure classification of an authored replacement against a captured catalog entry.</summary>
    public static class CityProceduralBatchAuditRules
    {
        public enum Status
        {
            SkippedOtherCity,
            MissingObjectId,
            MissingPrefab,
            MissingCatalogObject,
            WrongObjectKind,
            InvalidGeometry,
            Ready
        }

        public static Status Classify(string currentCity, string anchorCity, string objectId,
            bool hasPrefab, bool entryExists, bool kindMatches, bool geometryValid)
        {
            if (!string.Equals(currentCity, anchorCity, StringComparison.Ordinal))
                return Status.SkippedOtherCity;
            if (string.IsNullOrEmpty(objectId)) return Status.MissingObjectId;
            if (!hasPrefab) return Status.MissingPrefab;
            if (!entryExists) return Status.MissingCatalogObject;
            if (!kindMatches) return Status.WrongObjectKind;
            if (!geometryValid) return Status.InvalidGeometry;
            return Status.Ready;
        }
    }
}
