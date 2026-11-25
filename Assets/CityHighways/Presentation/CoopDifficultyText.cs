using CityHighways.Core;

namespace CityHighways.Presentation
{
    public static class CoopDifficultyText
    {
        static bool En => LocalizationService.Language == "en";
        public static string Title(int value)
        {
            switch (CooperativeDifficulty.Normalize(value))
            {
                case CoopDifficultyKind.Easy: return En ? "Easy" : "Лёгкая";
                case CoopDifficultyKind.Hard: return En ? "Hard" : "Сложная";
                default: return En ? "Normal" : "Обычная";
            }
        }

        public static string Hint(int value)
        {
            switch (CooperativeDifficulty.Normalize(value))
            {
                case CoopDifficultyKind.Easy: return En ? "Longer deadlines, slower crisis. Local records only." : "Больше времени, медленнее кризис. Только местные рекорды.";
                case CoopDifficultyKind.Hard: return En ? "Shorter deadlines, faster crisis. Local records only." : "Меньше времени, быстрее кризис. Только местные рекорды.";
                default: return En ? "Original co-op rules; cloud records available through Steam." : "Исходные правила; облачные рекорды через Steam.";
            }
        }
    }
}
