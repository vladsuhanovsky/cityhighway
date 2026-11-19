using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Система слоёв персонажа. Персонаж у нас не 3D-модель со скелетом, а стопка PNG 512×768 на одном холсте,
    /// поэтому «сокеты», «конфликты» и «скелетная одежда» из 3D-игр устроены здесь так:
    /// <list type="bullet">
    /// <item>Слот — ровно одна вещь (<see cref="Outfit"/> хранит слот → id, при любой смене наряд проверяется заново:
    /// вещи чужого пола и чужого слота выбрасываются, пустые обязательные слоты получают вещь по умолчанию).</item>
    /// <item>Сокеты — точки привязки слотов на холсте (голова, грудь, пояс, ноги, рука). Вокруг них — анимация
    /// надевания, по ним рисуется отладочная разметка (F3 во вкладке «Персонаж»).</item>
    /// <item>Конфликты: головной убор прячет волосы, которые торчали бы из-под него (пучок над кепкой, хвост над
    /// беретом, кудри над полями шляпы); комбинезон рисуется поверх футболки, но под верхней одеждой.</item>
    /// </list>
    /// </summary>
    public static class CharacterLayers
    {
        public const float CanvasWidth = 512f, CanvasHeight = 768f;

        public enum Socket { Head, Chest, Hips, Feet, Hand, Body }

        /// <summary>Сокет слота.</summary>
        public static Socket SocketOf(string slot)
        {
            switch (slot)
            {
                case "hair":
                case "hat": return Socket.Head;
                case "top": return Socket.Chest;
                case "bottom": return Socket.Hips;
                case "shoes": return Socket.Feet;
                case "accessory": return Socket.Hand;
                default: return Socket.Body;
            }
        }

        /// <summary>Положение сокета на холсте в долях (0…1, сверху вниз). Фигуры обоих полов стоят одинаково.</summary>
        public static Vector2 SocketPoint(Socket socket, string gender)
        {
            bool female = gender == CharacterCatalog.Female;
            switch (socket)
            {
                case Socket.Head: return new Vector2(0.50f, female ? 0.11f : 0.10f);
                case Socket.Chest: return new Vector2(0.50f, female ? 0.27f : 0.28f);
                case Socket.Hips: return new Vector2(0.50f, female ? 0.52f : 0.50f);
                case Socket.Feet: return new Vector2(0.50f, 0.91f);
                case Socket.Hand: return new Vector2(female ? 0.27f : 0.24f, female ? 0.55f : 0.57f);
                default: return new Vector2(0.50f, 0.50f);
            }
        }

        public static readonly Socket[] AllSockets = { Socket.Head, Socket.Chest, Socket.Hips, Socket.Feet, Socket.Hand };

        public static string SocketTitle(Socket socket)
        {
            switch (socket)
            {
                case Socket.Head: return "голова";
                case Socket.Chest: return "грудь";
                case Socket.Hips: return "пояс";
                case Socket.Feet: return "ноги";
                case Socket.Hand: return "рука";
                default: return "тело";
            }
        }

        /// <summary>Рамка вещи на холсте в долях (из catalog.json; нет — весь холст).</summary>
        public static Rect Bounds(CharacterItem item)
        {
            var b = item?.bounds;
            if (b == null || b.Length < 4 || b[2] <= 0 || b[3] <= 0) return new Rect(0f, 0f, 1f, 1f);
            return new Rect(b[0] / CanvasWidth, b[1] / CanvasHeight, b[2] / CanvasWidth, b[3] / CanvasHeight);
        }

        /// <summary>Точка, вокруг которой анимируется вещь: центр её рамки, а без рамки — сокет слота.</summary>
        public static Vector2 Pivot(CharacterItem item, string slot, string gender)
        {
            if (item?.bounds != null && item.bounds.Length >= 4) return Bounds(item).center;
            return SocketPoint(SocketOf(slot), gender);
        }

        // ------------------------------------------------------------------ порядок слоёв

        /// <summary>
        /// Порядок рисования слоёв наряда снизу вверх: базовый из catalog.json, но низ с «overTop» (комбинезон)
        /// поднимается над верхом, если верх — не верхняя одежда («outer»: жилет, пиджак, кардиган, жакет).
        /// </summary>
        public static List<string> DrawOrder(Outfit outfit)
        {
            var order = new List<string>(CharacterCatalog.DrawOrder);
            var bottom = CharacterCatalog.Item(outfit?.Get("bottom"));
            var top = CharacterCatalog.Item(outfit?.Get("top"));
            if (bottom != null && bottom.overTop && (top == null || !top.outer))
            {
                order.Remove("bottom");
                int index = order.IndexOf("top");
                order.Insert(index < 0 ? order.Count : index + 1, "bottom");
            }
            return order;
        }

        /// <summary>Расставить слои-элементы в порядке наряда (элементы — дети одного родителя).</summary>
        public static void Arrange(Outfit outfit, IDictionary<string, VisualElement> layers)
        {
            foreach (var slot in DrawOrder(outfit))
                if (layers.TryGetValue(slot, out var layer)) layer.BringToFront();
        }

        // ------------------------------------------------------------------ волосы под головным убором

        // Ключи — сами текстуры (GetInstanceID в Unity 6 устарел и запрещён): волосы → головной убор → результат.
        static readonly Dictionary<Texture2D, Dictionary<Texture2D, Texture2D>> HairCache = new Dictionary<Texture2D, Dictionary<Texture2D, Texture2D>>();
        static readonly Dictionary<Texture2D, HatShape> HatCache = new Dictionary<Texture2D, HatShape>();

        /// <summary>Силуэт головного убора: верхний край по столбцам и линия полей (в долях высоты холста).</summary>
        sealed class HatShape
        {
            public float[] Top;   // по столбцам, -1 — в столбце шляпы нет
            public float Brim;
        }

        /// <summary>
        /// Волосы под головным убором: всё, что выше силуэта шляпы в её столбцах и выше линии полей сбоку от неё,
        /// убирается (сбоку — с мягким краем). Длинные волосы ниже полей остаются. Результат кэшируется.
        /// Не получилось прочитать картинки — возвращаются волосы как есть.
        /// </summary>
        public static Texture2D HairUnderHat(Texture2D hair, Texture2D hat)
        {
            if (hair == null || hat == null) return hair;
            if (!HairCache.TryGetValue(hair, out var byHat)) HairCache[hair] = byHat = new Dictionary<Texture2D, Texture2D>();
            if (byHat.TryGetValue(hat, out var cached)) return cached ?? hair;
            Texture2D result = null;
            try
            {
                var shape = Shape(hat);
                if (shape != null) result = Mask(hair, shape);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("City Highways: не удалось подрезать волосы под головной убор: " + e.Message);
            }
            byHat[hat] = result;
            return result ?? hair;
        }

        static HatShape Shape(Texture2D hat)
        {
            if (HatCache.TryGetValue(hat, out var cached)) return cached;
            var pixels = Pixels(hat, out int w, out int h);
            HatShape shape = null;
            if (pixels != null)
            {
                // Смотрим только верх холста (голова): у шляпки лента может свисать ниже — она не поля.
                int limit = Mathf.RoundToInt(h * 0.40f);
                var top = new float[w];
                var bottoms = new List<int>();
                for (int x = 0; x < w; x++)
                {
                    int first = -1, last = -1;
                    for (int ty = 0; ty < limit; ty++)
                    {
                        // В Unity строка 0 — низ картинки; ty — от верха.
                        if (pixels[(h - 1 - ty) * w + x].a <= 128) continue;
                        if (first < 0) first = ty;
                        last = ty;
                    }
                    top[x] = first < 0 ? -1f : first / (float)h;
                    if (last >= 0) bottoms.Add(last);
                }
                if (bottoms.Count > 0)
                {
                    bottoms.Sort();
                    shape = new HatShape { Top = top, Brim = bottoms[bottoms.Count / 2] / (float)h };
                }
            }
            HatCache[hat] = shape;
            return shape;
        }

        static Texture2D Mask(Texture2D hair, HatShape shape)
        {
            var pixels = Pixels(hair, out int w, out int h);
            if (pixels == null) return null;
            float scale = h / CanvasHeight;
            float under = 3f * scale;      // под краем шляпы — запас, чтобы по контуру не светились волосы
            float feather = 10f * scale;   // мягкий край сбоку от шляпы
            float brim = shape.Brim * h;
            int hatWidth = shape.Top.Length;
            for (int x = 0; x < w; x++)
            {
                float hatTop = shape.Top[Mathf.Clamp(x * hatWidth / w, 0, hatWidth - 1)];
                for (int ty = 0; ty < h; ty++)
                {
                    int index = (h - 1 - ty) * w + x;
                    var c = pixels[index];
                    if (c.a == 0) continue;
                    float keep = hatTop >= 0f
                        ? (ty >= hatTop * h + under ? 1f : 0f)
                        : Mathf.Clamp01((ty - brim) / feather + 1f);
                    if (keep >= 1f) continue;
                    c.a = (byte)Mathf.RoundToInt(c.a * keep);
                    pixels[index] = c;
                }
            }
            var result = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
            {
                name = hair.name + "_underhat",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave
            };
            result.SetPixels32(pixels);
            result.Apply(true, false);
            return result;
        }

        /// <summary>
        /// Пиксели картинки. Слои гардероба импортируются без чтения на процессоре (и сжатыми), поэтому, если
        /// напрямую нельзя, картинка копируется через видеокарту: Blit во временную текстуру и ReadPixels.
        /// </summary>
        static Color32[] Pixels(Texture2D texture, out int width, out int height)
        {
            width = texture.width;
            height = texture.height;
            if (texture.isReadable)
            {
                try
                {
                    return texture.GetPixels32();
                }
                catch (System.Exception)
                {
                    // Сжатый формат без чтения на процессоре — читаем через видеокарту.
                }
            }
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                copy.Apply(false, false);
                var pixels = copy.GetPixels32();
                Object.Destroy(copy);
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        // ------------------------------------------------------------------ отладка

        /// <summary>Показывать сокеты и рамки вещей поверх персонажа (F3 во вкладке «Персонаж»).</summary>
        public static bool ShowDebug;
    }

    /// <summary>
    /// Отладочная разметка поверх персонажа: точки сокетов с подписями и рамки надетых вещей своим цветом
    /// для каждого слота. Видна, пока включён <see cref="CharacterLayers.ShowDebug"/>.
    /// </summary>
    public sealed class CharacterDebugOverlay : VisualElement
    {
        Outfit _outfit;
        readonly List<Label> _labels = new List<Label>();

        static readonly Dictionary<string, Color> SlotColors = new Dictionary<string, Color>
        {
            { "hair", new Color(0.95f, 0.75f, 0.30f) },
            { "hat", new Color(1.00f, 0.35f, 0.35f) },
            { "top", new Color(0.35f, 0.85f, 0.45f) },
            { "bottom", new Color(0.35f, 0.60f, 1.00f) },
            { "shoes", new Color(0.95f, 0.90f, 0.30f) },
            { "accessory", new Color(0.85f, 0.45f, 0.95f) },
        };

        public CharacterDebugOverlay()
        {
            pickingMode = PickingMode.Ignore;
            Ui.Fill(this);
            generateVisualContent += Draw;
            foreach (var socket in CharacterLayers.AllSockets)
            {
                var label = new Label(CharacterLayers.SocketTitle(socket)) { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.fontSize = 9f;
                label.style.color = Color.white;
                label.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
                label.style.paddingLeft = 2f;
                label.style.paddingRight = 2f;
                label.userData = socket;
                _labels.Add(label);
                Add(label);
            }
            RegisterCallback<GeometryChangedEvent>(_ => Place());
        }

        public void Set(Outfit outfit)
        {
            _outfit = outfit;
            style.display = CharacterLayers.ShowDebug ? DisplayStyle.Flex : DisplayStyle.None;
            Place();
            MarkDirtyRepaint();
        }

        void Place()
        {
            var size = layout.size;
            if (float.IsNaN(size.x) || size.x <= 0f) return;
            string gender = _outfit?.Gender ?? CharacterCatalog.Male;
            foreach (var label in _labels)
            {
                var p = CharacterLayers.SocketPoint((CharacterLayers.Socket)label.userData, gender);
                label.style.left = p.x * size.x + 5f;
                label.style.top = p.y * size.y - 6f;
            }
        }

        void Draw(MeshGenerationContext context)
        {
            var size = layout.size;
            if (_outfit == null || float.IsNaN(size.x) || size.x <= 0f) return;
            var p = context.painter2D;
            foreach (var pair in SlotColors)
            {
                var item = CharacterCatalog.Item(_outfit.Get(pair.Key));
                if (item == null) continue;
                var b = CharacterLayers.Bounds(item);
                p.strokeColor = pair.Value;
                p.lineWidth = 1f;
                p.BeginPath();
                p.MoveTo(new Vector2(b.xMin * size.x, b.yMin * size.y));
                p.LineTo(new Vector2(b.xMax * size.x, b.yMin * size.y));
                p.LineTo(new Vector2(b.xMax * size.x, b.yMax * size.y));
                p.LineTo(new Vector2(b.xMin * size.x, b.yMax * size.y));
                p.ClosePath();
                p.Stroke();
            }
            foreach (var socket in CharacterLayers.AllSockets)
            {
                var point = CharacterLayers.SocketPoint(socket, _outfit.Gender);
                p.fillColor = new Color(1f, 0.2f, 0.2f, 0.9f);
                p.BeginPath();
                p.Arc(new Vector2(point.x * size.x, point.y * size.y), 3.5f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }
        }
    }
}
