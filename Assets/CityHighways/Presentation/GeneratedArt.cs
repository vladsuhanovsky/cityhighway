using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Картинки, сгенерированные в Scenario и положенные в Resources (см. README, «Арт из Scenario»).
    /// Каждый файл необязателен: нет файла — игра рисует как раньше (процедурно или буквами).
    /// Пути — без расширения, внутри Resources.
    /// </summary>
    public static class GeneratedArt
    {
        public const string CargoIcons = "CityHighways/UI/cargo_icons";
        public const string HubEventIcons = "CityHighways/UI/hub_event_icons";
        public const string KeyArt = "CityHighways/UI/key_art";
        /// <summary>Экран загрузки магната: логистический город на закате (Scenario, GPT Image 2.5).</summary>
        public const string MagnateLoading = "CityHighways/UI/magnate_loading";
        /// <summary>Экран загрузки соревнования: стройка дорог наперегонки (Scenario, GPT Image 2.5).</summary>
        public const string CompetitionLoading = "CityHighways/UI/competition_loading";
        /// <summary>Экран загрузки кооператива: все компании строят одну сеть против кризиса (Scenario, GPT Image 2.5).</summary>
        public const string CooperativeLoading = "CityHighways/UI/coop_loading";
        public const string GroundTexture = "CityHighways/City3D/Textures/ground";
        public const string AsphaltTexture = "CityHighways/City3D/Textures/asphalt";
        public const string PavingTexture = "CityHighways/City3D/Textures/paving";
        public const string RoofTexture = "CityHighways/City3D/Textures/roof";
        public const string CardArtFolder = "CityHighways/Cards/Art/";

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Текстура из Resources или null (результат кэшируется, в том числе отсутствие).</summary>
        public static Texture2D Load(string path)
        {
            if (Cache.TryGetValue(path, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(path);
            Cache[path] = texture;
            return texture;
        }

        /// <summary>
        /// Пиксели текстуры, уменьшенной до width × height. Через RenderTexture: импортированные картинки
        /// обычно нечитаемы с CPU (Read/Write выключен), а Blit работает всегда — и в редакторе, и в сборке.
        /// </summary>
        public static Color[] ReadPixels(Texture2D source, int width, int height)
        {
            if (source == null) return null;
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply(false, false);
                var pixels = copy.GetPixels();
                Object.Destroy(copy);
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }

    /// <summary>Лист иконок: count квадратных ячеек в один ряд; ячейка i — спрайт i (квадрат по центру ячейки).</summary>
    public sealed class IconSheet
    {
        readonly string _path;
        readonly int _count;
        Sprite[] _sprites;
        bool _loaded;

        public IconSheet(string path, int count)
        {
            _path = path;
            _count = count;
        }

        public bool Available => Sprites() != null;

        public Sprite Get(int index)
        {
            var sprites = Sprites();
            return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
        }

        /// <summary>Элемент с иконкой (size × size) или null, если листа нет.</summary>
        public VisualElement Image(int index, float size)
        {
            var sprite = Get(index);
            if (sprite == null) return null;
            var image = new VisualElement();
            image.style.width = size;
            image.style.height = size;
            image.style.flexShrink = 0f;
            image.style.backgroundImage = new StyleBackground(sprite);
            image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            image.pickingMode = PickingMode.Ignore;
            return image;
        }

        Sprite[] Sprites()
        {
            if (_loaded) return _sprites;
            _loaded = true;
            var texture = GeneratedArt.Load(_path);
            if (texture == null) return null;
            float cell = texture.width / (float)_count;
            float side = Mathf.Min(cell, texture.height);
            _sprites = new Sprite[_count];
            for (int i = 0; i < _count; i++)
            {
                var rect = new Rect(i * cell + (cell - side) * 0.5f, (texture.height - side) * 0.5f, side, side);
                _sprites[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _sprites[i].name = $"{texture.name} {i}";
            }
            return _sprites;
        }
    }

    /// <summary>Иконки хабов (0 аэропорт, 1 вокзал, 2 порт) и событий магната (3–8 в порядке LogisticsEventKind).</summary>
    public static class HubEventIcons
    {
        static readonly IconSheet Sheet = new IconSheet(GeneratedArt.HubEventIcons, 9);

        public static VisualElement Hub(Core.HubKind kind, float size) =>
            Sheet.Image(kind == Core.HubKind.Airport ? 0 : kind == Core.HubKind.Station ? 1 : 2, size);

        public static VisualElement Event(Core.LogisticsEventKind kind, float size) => Sheet.Image(3 + (int)kind, size);
    }
}
