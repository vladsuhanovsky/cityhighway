using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>Language-neutral character rules remain in Core; only presentation strings are translated.</summary>
    public static class CharacterLocalization
    {
        static string T(string key, string fallback) => LocalizationService.GetOrFallback("character." + key, fallback);

        public static string Title(CharacterKind kind) => T("title." + kind, Characters.Title(kind));
        public static string Quote(CharacterKind kind) => T("quote." + kind, Characters.Quote(kind));
        public static string Hint(CharacterKind kind) => T("hint." + kind, Characters.Hint(kind));
        public static string DifficultyTitle(int level) => T("difficulty." + level, Characters.DifficultyTitle(level));

        public static string Ability(CharacterKind kind, GameConfig config)
        {
            switch (kind)
            {
                case CharacterKind.Foreman:
                    return string.Format(T(config.ForemanFreeRoads == 1 ? "ability.ForemanOnce" : "ability.ForemanMany",
                        Characters.Ability(kind, config)), config.ForemanFreeRoads, config.ForemanMaxLength);
                case CharacterKind.Insurer:
                    return string.Format(T("ability.Insurer", Characters.Ability(kind, config)), config.InsurerFine);
                case CharacterKind.Tollman:
                    return string.Format(T("ability.Tollman", Characters.Ability(kind, config)), config.TollmanToll, config.TollPerRoad);
                case CharacterKind.Official:
                    return string.Format(T("ability.Official", Characters.Ability(kind, config)), config.OfficialBonusPerContract);
                case CharacterKind.Bridger:
                    return string.Format(T("ability.Bridger", Characters.Ability(kind, config)), config.BridgerDiscount, config.BridgerPointsPerBridge);
                default:
                    return T("ability." + kind, Characters.Ability(kind, config));
            }
        }
    }
}
