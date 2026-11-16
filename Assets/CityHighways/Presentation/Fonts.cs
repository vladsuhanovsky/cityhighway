using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Шрифты игры (Resources/CityHighways/Fonts, тексты лицензий лежат рядом).
    /// Основной — Roboto: заголовки Roboto Bold, текст Roboto Regular, цифры Roboto Mono.
    /// Если файлов Roboto в папке нет, берутся запасные: Golos Text (текст и заголовки) и DejaVu Sans Mono (цифры).
    /// Чтобы включить Roboto, положите в папку Roboto-Regular.ttf, Roboto-Medium.ttf, Roboto-Bold.ttf,
    /// RobotoMono-Regular.ttf и RobotoMono-Bold.ttf (Google Fonts, лицензия SIL OFL / Apache 2.0) — код менять не нужно.
    /// Russo One остаётся только для логотипа. Жирность задаётся выбором начертания, а не FontStyle.Bold —
    /// иначе Unity дорисовывает «фальшивый» жирный.
    /// </summary>
    public static class Fonts
    {
        public enum Weight { Regular, SemiBold, Bold, Display, Mono, MonoBold, Logo }

        /// <summary>Крупнее этого жирный текст набирается заголовочным начертанием.</summary>
        public const float DisplayFrom = 18f;

        const string Folder = "CityHighways/Fonts/";
        static Font _regular, _semiBold, _bold, _mono, _monoBold, _logo;
        static bool _loaded;

        /// <summary>Первый найденный файл из списка.</summary>
        static Font First(params string[] names)
        {
            foreach (var name in names)
            {
                var font = Resources.Load<Font>(Folder + name);
                if (font != null) return font;
            }
            return null;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _regular = First("Roboto-Regular", "GolosText-Regular");
            _semiBold = First("Roboto-Medium", "GolosText-SemiBold", "Roboto-Bold");
            _bold = First("Roboto-Bold", "GolosText-Bold");
            _mono = First("RobotoMono-Regular", "RobotoMono-Medium", "DejaVuSansMono");
            _monoBold = First("RobotoMono-Bold", "RobotoMono-Medium", "DejaVuSansMono-Bold", "DejaVuSansMono");
            _logo = First("RussoOne-Regular", "Roboto-Bold", "GolosText-Bold");
        }

        public static Font Get(Weight weight)
        {
            Load();
            switch (weight)
            {
                case Weight.Logo: return _logo ?? _bold ?? _regular;
                case Weight.Display:
                case Weight.Bold: return _bold ?? _semiBold ?? _regular;
                case Weight.SemiBold: return _semiBold ?? _bold ?? _regular;
                case Weight.Mono: return _mono ?? _regular;
                case Weight.MonoBold: return _monoBold ?? _mono ?? _bold ?? _regular;
                default: return _regular;
            }
        }

        public static Font Regular => Get(Weight.Regular);

        /// <summary>Начертание для текста такого размера и жирности: заголовки (18–24) — Bold, мелкий жирный — Medium.</summary>
        public static Weight For(float size, bool bold) =>
            !bold ? Weight.Regular : size >= DisplayFrom ? Weight.Display : size >= 15f ? Weight.Bold : Weight.SemiBold;

        /// <summary>
        /// Назначить элементу начертание. Если файла шрифта нет — оставляем шрифт по умолчанию
        /// и жирность через FontStyle.Bold, как было.
        /// </summary>
        public static void Apply(VisualElement element, Weight weight)
        {
            var font = Get(weight);
            if (font == null)
            {
                element.style.unityFontStyleAndWeight = weight == Weight.Regular || weight == Weight.Mono ? FontStyle.Normal : FontStyle.Bold;
                return;
            }
            element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(font));
            element.style.unityFontStyleAndWeight = FontStyle.Normal;
            // Логотип плотный: чуть разрядки для читаемости.
            if (weight == Weight.Logo) element.style.letterSpacing = 0.5f;
        }
    }
}
