using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Иконки видов груза из листа Resources/CityHighways/UI/cargo_icons.png (сгенерирован в Scenario): семь квадратных
    /// ячеек в ряд, в порядке CargoKind — обычный, скоропорт, тяжеловес, опасный, хрупкий, ценный, негабарит.
    /// Листа нет — Get возвращает null, и чипы рисуются как раньше: буква на цветном квадрате.
    /// </summary>
    public static class CargoIcons
    {
        public const string ResourcePath = GeneratedArt.CargoIcons;
        static readonly IconSheet Sheet = new IconSheet(ResourcePath, 7);

        public static bool Available => Sheet.Available;

        public static Sprite Get(CargoKind kind) => Sheet.Get((int)kind);

        /// <summary>
        /// Чип вида груза: иконка из листа или (если листа нет) буква на цветном квадрате. size — сторона в пикселях.
        /// </summary>
        public static Label Chip(CargoKind kind, float size, float letterScale = 0.6f, float radius = 4f)
        {
            var sprite = Get(kind);
            var chip = Ui.Text(sprite != null ? "" : MagnateView.KindLetter(kind), size * letterScale, new Color(0.08f, 0.08f, 0.1f), true);
            chip.style.width = size;
            chip.style.height = size;
            chip.style.flexShrink = 0f;
            chip.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (sprite != null)
            {
                chip.style.backgroundImage = new StyleBackground(sprite);
                chip.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            }
            else
            {
                chip.style.backgroundColor = MagnateView.KindColor(kind);
                Ui.Radius(chip, radius);
            }
            return chip;
        }
    }
}
