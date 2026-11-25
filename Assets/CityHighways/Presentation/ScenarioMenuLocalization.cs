using System.Collections.Generic;
using CityHighways.Core;

// Keep scenario UI helpers internal while allowing EditMode regression tests.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CityHighways.Tests")]

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
                case ScenarioKind.SupplySeason: return T("supply");
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
                case ScenarioKind.SupplySeason: return T("supply_summary");
                default: return T("standard_summary");
            }
        }

        // Translate only exact scenario announcements from Core, preserving custom rule values.
        // Source journal entries are never rewritten, so RU/EN can switch mid-game.
        public static string JournalAnnouncement(string message, ScenarioKind kind, GameConfig config)
        {
            if (LocalizationService.Language != "en" || kind == ScenarioKind.Standard || config == null)
                return null;
            string original = "Сценарий «" + Scenarios.Title(kind) + "»: " +
                string.Join(" ", Scenarios.Changes(kind, config));
            if (!string.Equals(message, original, System.StringComparison.Ordinal)) return null;
            return "Scenario “" + Title(kind) + "”: " + string.Join(" ", Changes(kind, config));
        }

        public static IReadOnlyList<string> Changes(ScenarioKind kind, GameConfig config = null)
        {
            // Use actual game rules for historical journal lines; menus use scenario defaults.
            GameConfig c = config ?? Scenarios.Apply(new GameConfig(), kind);
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
                case ScenarioKind.SupplySeason:
                    return new[]
                    {
                        string.Format(T("supply_change_1"), c.AsphaltDeliveryEvents, c.AsphaltDeliveryDiscount),
                        string.Format(T("supply_change_2"), c.ConcreteShortageEvents, c.ConcreteShortageExtraCost),
                        T("supply_change_3")
                    };
                default: return new[] { T("standard_summary") };
            }
        }
    }
}
