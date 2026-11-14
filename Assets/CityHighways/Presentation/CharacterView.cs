using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Персонаж из слоёв одежды. Полный рост — прямоугольник 2:3, как холст картинок (512×768).
    /// Портрет — квадрат с головой и плечами (верхние 40% холста по центру).
    /// </summary>
    public sealed class CharacterView : VisualElement
    {
        readonly VisualElement _stage;
        readonly Dictionary<string, VisualElement> _layers = new Dictionary<string, VisualElement>();

        public CharacterView(float width, bool portrait = false)
        {
            pickingMode = PickingMode.Ignore;
            style.flexShrink = 0f;
            style.overflow = Overflow.Hidden;
            _stage = new VisualElement { pickingMode = PickingMode.Ignore };
            _stage.style.position = Position.Absolute;
            if (portrait)
            {
                style.width = width;
                style.height = width;
                // Область 0,2…0,8 по ширине и 0…0,4 по высоте холста — квадрат.
                float stageWidth = width / 0.6f;
                _stage.style.width = stageWidth;
                _stage.style.height = stageWidth * 1.5f;
                _stage.style.left = -0.2f * stageWidth;
                _stage.style.top = 0f;
            }
            else
            {
                style.width = width;
                style.height = width * 1.5f;
                _stage.style.width = width;
                _stage.style.height = width * 1.5f;
                _stage.style.left = 0f;
                _stage.style.top = 0f;
            }
            Add(_stage);

            foreach (var slot in CharacterCatalog.DrawOrder)
            {
                var layer = new VisualElement { pickingMode = PickingMode.Ignore };
                Ui.Fill(layer);
                layer.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                _stage.Add(layer);
                _layers[slot] = layer;
            }
        }

        public void Set(Outfit outfit)
        {
            outfit = outfit ?? Outfit.Default;
            foreach (var pair in _layers)
            {
                var texture = CharacterCatalog.Texture(CharacterCatalog.Item(outfit.Get(pair.Key)));
                pair.Value.style.backgroundImage = texture != null ? new StyleBackground(texture) : new StyleBackground(StyleKeyword.None);
            }
        }
    }
}
