using System.Collections.Generic;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Translated scenario labels for the new-game setup only.
    /// Do not localize Core.Scenarios: rule settings and multiplayer state must remain language neutral.
    /// </summary>
    internal static class ScenarioMenuLocalization
    {
        static string T(string key) => LocalizationService.Get("menu.scenario." + key);

        public static string Title(ScenarioKind kind)
        {
            switch (kind)
            {
                case ScenarioKind.Winter: return T("winter");
                case ScenarioKind.GreaterMoscow: return T("greater");
                case ScenarioKind.RepairYear: return T("repairs");
                case ScenarioKind.Elections: return T("elections");
                default: return T("standard");
            }
        }

        public static string Summary(ScenarioKind kind)
        {
            switch (kind)
            {
                case ScenarioKind.Winter: return T("winter_summary");
                case ScenarioKind.GreaterMoscow: return T("greater_summary");
                case ScenarioKind.RepairYear: return T("repairs_summary");
                case ScenarioKind.Elections: return T("elections_summary");
                default: return T("standard_summary");
            }
        }

        public static IReadOnlyList<string> Changes(ScenarioKind kind)
        {
            // The same config values as Core.Scenarios.Changes; only human-readable text is localized.
            GameConfig c = Scenarios.Apply(new GameConfig(), kind);
            switch (kind)
            {
                case ScenarioKind.Winter:
                    return new[]
                    {
                        string.Format(T("winter_change_1"), c.SnowExtraCost),
                        string.Format(T("winter_change_2"), c.SnowExtraCost),
                        T("winter_change_3"),
                        string.Format(T("winter_change_4"), c.CardsPerResource[0], c.CardsPerResource[3], c.MachineryCards)
                    };
                case ScenarioKind.GreaterMoscow:
                    return new[]
                    {
                        T("greater_change_1"),
                        string.Format(T("greater_change_2"), c.ExtraRoadPieces),
                        string.Format(T("greater_change_3"), c.InitialContractsDealt, c.InitialContractsMinKeep)
                    };
                case ScenarioKind.RepairYear:
                    return new[]
                    {
                        string.Format(T("repairs_change_1"), c.ScheduledRepairs),
                        T("repairs_change_2"),
                        T("repairs_change_3"),
                        string.Format(T("repairs_change_4"), c.InfluencePerLongRoad, c.LongRoadLength)
                    };
                case ScenarioKind.Elections:
                    return new[]
                    {
                        string.Format(T("elections_change_1"), (int)(c.SecondAuctionShare * 100)),
                        string.Format(T("elections_change_2"), c.InfluencePerContract, c.InfluencePerComplexContract, c.InfluencePerNewDistrict),
                        string.Format(T("elections_change_3"), c.BribeDiscountCost),
                        string.Format(T("elections_change_4"), c.MayorFriendBonus)
                    };
                default: return new[] { T("standard_summary") };
            }
        }
    }
}
