using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Item-only thumbnail from the existing catalog layer, cropped to its authored
    /// bounds without creating textures, CPU readback or changing import settings.</summary>
    public sealed class WardrobeItemPreview : VisualElement
    {
        public static Rect Crop(CharacterItem item)
        {
            var b = CharacterLayers.Bounds(item);
            float x0 = Mathf.Clamp01(b.xMin), y0 = Mathf.Clamp01(b.yMin);
            float x1 = Mathf.Clamp01(b.xMax), y1 = Mathf.Clamp01(b.yMax);
            return x1 > x0 && y1 > y0 ? Rect.MinMaxRect(x0, y0, x1, y1) : new Rect(0, 0, 1, 1);
        }

        public WardrobeItemPreview(CharacterItem item, string tone, float width = 92f, float height = 82f)
        {
            pickingMode = PickingMode.Ignore;
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.backgroundColor = GameColors.Background;
            Ui.Radius(this, 8f);
            var texture = CharacterCatalog.Texture(item, tone);
            if (texture == null)
            {
                Add(Ui.Text("—", 22f, Theme.TextDim, true));
                return;
            }
            Rect crop = Crop(item);
            float scale = Mathf.Min((width - 10f) / (crop.width * CharacterLayers.CanvasWidth),
                (height - 10f) / (crop.height * CharacterLayers.CanvasHeight));
            float fullWidth = CharacterLayers.CanvasWidth * scale, fullHeight = CharacterLayers.CanvasHeight * scale;
            var clip = new VisualElement { pickingMode = PickingMode.Ignore };
            clip.style.width = crop.width * fullWidth;
            clip.style.height = crop.height * fullHeight;
            clip.style.overflow = Overflow.Hidden;
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = -crop.x * fullWidth;
            layer.style.top = -crop.y * fullHeight;
            layer.style.width = fullWidth;
            layer.style.height = fullHeight;
            layer.style.backgroundImage = new StyleBackground(texture);
            layer.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            clip.Add(layer);
            Add(clip);
        }
    }
}
