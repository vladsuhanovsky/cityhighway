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
        public const string ResourcePath = "CityHighways/UI/cargo_icons";
        const int Count = 7;

        static Sprite[] s_sprites;
        static bool s_loaded;

        public static bool Available => Sprites() != null;

        public static Sprite Get(CargoKind kind)
        {
            var sprites = Sprites();
            int index = (int)kind;
            return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
        }

        static Sprite[] Sprites()
        {
            if (s_loaded) return s_sprites;
            s_loaded = true;
            var texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null) return null;
            float cell = texture.width / (float)Count;
            float side = Mathf.Min(cell, texture.height);
            s_sprites = new Sprite[Count];
            for (int i = 0; i < Count; i++)
            {
                // Квадрат по центру ячейки: лист может быть чуть выше или ниже ряда иконок.
                var rect = new Rect(i * cell + (cell - side) * 0.5f, (texture.height - side) * 0.5f, side, side);
                s_sprites[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                s_sprites[i].name = "Cargo " + (CargoKind)i;
            }
            return s_sprites;
        }

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
