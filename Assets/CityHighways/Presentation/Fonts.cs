using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Шрифты игры (Resources/CityHighways/Fonts, лицензия SIL OFL — тексты лицензий лежат рядом):
    /// Golos Text — основной текст (обычный, полужирный, жирный; статичные начертания сделаны из вариативного шрифта),
    /// Russo One — крупные заголовки, в духе дорожных указателей. Если файлов нет, остаётся шрифт по умолчанию.
    /// Жирность задаётся выбором начертания, а не FontStyle.Bold — иначе Unity дорисовывает «фальшивый» жирный.
    /// </summary>
    public static class Fonts
    {
        public enum Weight { Regular, SemiBold, Bold, Display }

        /// <summary>Крупнее этого жирный текст набирается заголовочным шрифтом.</summary>
        public const float DisplayFrom = 22f;

        static Font _regular, _semiBold, _bold, _display;
        static bool _loaded;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _regular = Resources.Load<Font>("CityHighways/Fonts/GolosText-Regular");
            _semiBold = Resources.Load<Font>("CityHighways/Fonts/GolosText-SemiBold");
            _bold = Resources.Load<Font>("CityHighways/Fonts/GolosText-Bold");
            _display = Resources.Load<Font>("CityHighways/Fonts/RussoOne-Regular");
        }

        public static Font Get(Weight weight)
        {
            Load();
            switch (weight)
            {
                case Weight.Display: return _display ?? _bold ?? _regular;
                case Weight.Bold: return _bold ?? _semiBold ?? _regular;
                case Weight.SemiBold: return _semiBold ?? _bold ?? _regular;
                default: return _regular;
            }
        }

        public static Font Regular => Get(Weight.Regular);

        /// <summary>Начертание для текста такого размера и жирности.</summary>
        public static Weight For(float size, bool bold) =>
            !bold ? Weight.Regular : size >= DisplayFrom ? Weight.Display : size >= 16f ? Weight.Bold : Weight.SemiBold;

        /// <summary>
        /// Назначить элементу начертание. Если файла шрифта нет — оставляем шрифт по умолчанию
        /// и жирность через FontStyle.Bold, как было.
        /// </summary>
        public static void Apply(VisualElement element, Weight weight)
        {
            var font = Get(weight);
            if (font == null)
            {
                element.style.unityFontStyleAndWeight = weight == Weight.Regular ? FontStyle.Normal : FontStyle.Bold;
                return;
            }
            element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            element.style.unityFontStyleAndWeight = FontStyle.Normal;
            // Заголовочный шрифт плотнее: чуть разрядки для читаемости.
            if (weight == Weight.Display) element.style.letterSpacing = 0.5f;
        }
    }
}
