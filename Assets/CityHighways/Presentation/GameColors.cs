using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Палитра страницы профиля (вкладки, карточки, кнопки, прогресс, график, редкость).
    /// Поменяйте цвет здесь — он изменится на всех вкладках профиля. Остальное меню и партия пока на <see cref="Theme"/>.
    /// </summary>
    public static class GameColors
    {
        public static Color Background = new Color(0.10f, 0.14f, 0.18f);      // #1A2332
        public static Color PanelBackground = new Color(0.18f, 0.24f, 0.30f); // #2E3D4D
        public static Color Accent = new Color(0.95f, 0.65f, 0.25f);          // #F3A640 (оранжевый)
        public static Color Button = new Color(0.25f, 0.35f, 0.45f);          // #405973
        public static Color ButtonHover = new Color(0.30f, 0.42f, 0.54f);     // #4D6B8A
        public static Color Success = new Color(0.20f, 0.80f, 0.40f);         // #33CC66
        public static Color Warning = new Color(0.95f, 0.75f, 0.20f);         // #F3BF33
        public static Color Danger = new Color(0.90f, 0.30f, 0.30f);          // #E64D4D

        // Редкость
        public static Color Common = new Color(0.70f, 0.70f, 0.70f);          // #B3B3B3
        public static Color Rare = new Color(0.20f, 0.60f, 0.90f);            // #3399E6
        public static Color Epic = new Color(0.60f, 0.30f, 0.90f);            // #994DE6
        public static Color Legendary = new Color(1.00f, 0.80f, 0.20f);       // #FFCC33

        // Производные: подобраны к палитре выше.
        /// <summary>Акцент при наведении — чуть светлее.</summary>
        public static Color AccentHover = new Color(1.00f, 0.74f, 0.38f);
        /// <summary>Тёмный текст на оранжевом: белый на #F3A640 читается плохо.</summary>
        public static Color AccentText = new Color(0.12f, 0.09f, 0.04f);
    }
}
