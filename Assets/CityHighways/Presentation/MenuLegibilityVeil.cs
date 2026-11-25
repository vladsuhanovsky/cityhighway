using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Soft non-interactive focus vignette behind the home menu.
    /// Buildings at the edges retain their window color; the center where
    /// labels live gets a gentle dark veil. Does not darken the pixel highway.
    /// </summary>
    public sealed class MenuLegibilityVeil : VisualElement
    {
        readonly ColorMesh _mesh = new ColorMesh();
        readonly float _strength;

        public MenuLegibilityVeil()
        {
            var style = MainMenuStyle.Current;
            _strength = style != null ? Mathf.Clamp(style.centerDarkening, 0f, 0.8f) : 0.5f;
            pickingMode = PickingMode.Ignore;
            Ui.Fill(this);
            generateVisualContent += Draw;
        }

        public static float Opacity(float x, float y) => Opacity(x, y, 0.5f);

        public static float Opacity(float x, float y, float strength)
        {
            // Normalized canvas coordinates. A smooth falloff prevents any
            // obvious rectangle underneath transparent menu buttons.
            float horizontal = Mathf.Clamp01(1f - Mathf.Abs(x - 0.5f) / 0.38f);
            float vertical = Mathf.Clamp01((y - 0.07f) / 0.20f) *
                             Mathf.Clamp01((0.81f - y) / 0.17f);
            float ease = horizontal * horizontal * (3f - 2f * horizontal);
            return Mathf.Clamp(strength, 0f, 0.8f) * ease * vertical;
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width < 50f || r.height < 50f) return;
            _mesh.Clear();
            const int columns = 16, rows = 12;
            for (int row = 0; row < rows; row++)
            {
                float y0 = row / (float)rows;
                float y1 = (row + 1f) / rows;
                for (int col = 0; col < columns; col++)
                {
                    float x0 = col / (float)columns;
                    float x1 = (col + 1f) / columns;
                    _mesh.Quad(
                        new Vector2(x0 * r.width, y0 * r.height),
                        new Vector2(x1 * r.width, y0 * r.height),
                        new Vector2(x1 * r.width, y1 * r.height),
                        new Vector2(x0 * r.width, y1 * r.height),
                        Shade(Opacity(x0, y0, _strength)), Shade(Opacity(x1, y0, _strength)),
                        Shade(Opacity(x1, y1, _strength)), Shade(Opacity(x0, y1, _strength)));
                }
            }
            _mesh.Flush(context);
        }

        static Color Shade(float opacity) =>
            new Color(0.015f, 0.023f, 0.041f, opacity);
    }
}
