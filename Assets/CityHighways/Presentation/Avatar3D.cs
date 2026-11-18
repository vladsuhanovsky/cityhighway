using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Готовый 3D-персонаж профиля (модель из Scenario, Rodin Gen-2.5).</summary>
    public sealed class Avatar3D
    {
        public string id;
        public string name;
        public string role;
        public string blurb;
        public Color accent;
    }

    /// <summary>
    /// Шесть готовых 3D-аватаров. Модели лежат в Resources/CityHighways/Avatars3D/&lt;id&gt; (GLB через glTFast или FBX, импортируется
    /// как префаб), картинка для выбора и запасной показ — &lt;id&gt;_portrait.png. Нет модели — показывается картинка,
    /// нет и картинки — буква на цветном круге. Пустой id — обычный 2D-персонаж из гардероба.
    /// </summary>
    public static class Avatar3DCatalog
    {
        public const string Folder = "CityHighways/Avatars3D/";

        public static readonly IReadOnlyList<Avatar3D> All = new[]
        {
            new Avatar3D { id = "trucker", name = "Михалыч", role = "Дальнобойщик", blurb = "Тридцать лет за рулём, знает каждую развязку.", accent = new Color(0.86f, 0.25f, 0.22f) },
            new Avatar3D { id = "dispatcher", name = "Алина", role = "Диспетчер", blurb = "Держит в голове все маршруты города.", accent = new Color(0.16f, 0.62f, 0.62f) },
            new Avatar3D { id = "engineer", name = "Сергей", role = "Инженер дорог", blurb = "Строит мосты быстрее, чем их успевают назвать.", accent = new Color(0.95f, 0.52f, 0.12f) },
            new Avatar3D { id = "crane", name = "Марина", role = "Крановщица порта", blurb = "Разгружает контейнеровоз за одну смену.", accent = new Color(0.24f, 0.36f, 0.72f) },
            new Avatar3D { id = "ceo", name = "Вера Павловна", role = "Директор компании", blurb = "Считает влияние до последней монеты.", accent = new Color(0.55f, 0.32f, 0.72f) },
            new Avatar3D { id = "courier", name = "Тёма", role = "Курьер", blurb = "Срежет через дворы — и уже на месте.", accent = new Color(0.27f, 0.62f, 0.32f) },
        };

        public static Avatar3D Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var avatar in All) if (avatar.id == id) return avatar;
            return null;
        }

        static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();

        /// <summary>Модель аватара (кэш, включая отсутствие); null — файла нет.</summary>
        public static GameObject Model(Avatar3D avatar)
        {
            if (avatar == null) return null;
            if (!Models.TryGetValue(avatar.id, out var model))
            {
                model = Resources.Load<GameObject>(Folder + avatar.id);
                Models[avatar.id] = model;
            }
            return model;
        }

        public static Texture2D Portrait(Avatar3D avatar) => avatar == null ? null : GeneratedArt.Load(Folder + avatar.id + "_portrait");

        /// <summary>Плитка-миниатюра: картинка персонажа или буква на круге цвета персонажа.</summary>
        public static VisualElement Thumbnail(Avatar3D avatar, float width, float height)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.width = width;
            box.style.height = height;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            var portrait = Portrait(avatar);
            if (portrait != null)
            {
                box.style.backgroundImage = new StyleBackground(portrait);
                box.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                return box;
            }
            float size = Mathf.Min(width, height) * 0.7f;
            var circle = new VisualElement { pickingMode = PickingMode.Ignore };
            circle.style.width = size;
            circle.style.height = size;
            circle.style.alignItems = Align.Center;
            circle.style.justifyContent = Justify.Center;
            circle.style.backgroundColor = avatar.accent;
            Ui.Radius(circle, size * 0.5f);
            var letter = Ui.Text(avatar.name.Substring(0, 1), size * 0.5f, Color.white, true);
            letter.pickingMode = PickingMode.Ignore;
            circle.Add(letter);
            box.Add(circle);
            return box;
        }
    }

    /// <summary>
    /// Площадка с 3D-аватаром: отдельная камера далеко от сцены рисует модель в RenderTexture, а элемент показывает её
    /// фоном. Персонаж медленно вращается сам, мышью его можно покрутить. Пока элемент не на экране, камера выключена.
    /// Нет модели — показывается картинка персонажа (лёгкое покачивание), нет и картинки — миниатюра-буква.
    /// </summary>
    public sealed class Avatar3DStage : VisualElement
    {
        const int Layer = 31;
        static readonly Vector3 Origin = new Vector3(5000f, -5000f, 5000f);
        const float ModelHeight = 1.8f;
        const float AutoSpin = 22f;

        readonly float _width, _height;
        readonly VisualElement _fallback;
        readonly VisualElement _shadow;
        GameObject _root, _pivot, _model;
        Camera _camera;
        Light _rim;
        RenderTexture _texture;
        Avatar3D _avatar;
        bool _attached;
        float _angle, _velocity, _time;
        bool _dragging;
        float _lastX;
        int _pointer = -1;

        public Avatar3DStage(float width, float height)
        {
            _width = width;
            _height = height;
            pickingMode = PickingMode.Position;
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);

            _shadow = new VisualElement { pickingMode = PickingMode.Ignore };
            _shadow.style.position = Position.Absolute;
            _shadow.style.bottom = 4f;
            _shadow.style.left = width * 0.2f;
            _shadow.style.width = width * 0.6f;
            _shadow.style.height = 16f;
            _shadow.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            Ui.Radius(_shadow, 8f);
            Add(_shadow);

            _fallback = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(_fallback);
            _fallback.style.alignItems = Align.Center;
            _fallback.style.justifyContent = Justify.Center;
            Add(_fallback);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _attached = true;
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _attached = false;
                if (_camera != null) _camera.enabled = false;
            });
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                _dragging = true;
                _pointer = e.pointerId;
                _lastX = e.position.x;
                this.CapturePointer(e.pointerId);
            });
            RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!_dragging || e.pointerId != _pointer) return;
                float dx = e.position.x - _lastX;
                _lastX = e.position.x;
                _angle += dx * 0.9f;
                _velocity = dx * 40f;
            });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != _pointer) return;
                _dragging = false;
                if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId);
            });
        }

        /// <summary>Показать аватара (null — пусто).</summary>
        public void Show(Avatar3D avatar)
        {
            if (avatar == _avatar && (_model != null || Avatar3DCatalog.Model(avatar) == null)) return;
            _avatar = avatar;
            _angle = 0f;
            _velocity = 0f;
            Refresh();
        }

        void Refresh()
        {
            var prefab = Avatar3DCatalog.Model(_avatar);
            if (prefab != null && _attached)
            {
                EnsureRig();
                if (_model == null || _model.name != _avatar.id) SpawnModel(prefab);
                _camera.enabled = true;
                _fallback.Clear();
                _fallback.style.display = DisplayStyle.None;
                style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_texture));
                return;
            }
            if (_camera != null) _camera.enabled = false;
            if (_model != null) Object.Destroy(_model);
            _model = null;
            style.backgroundImage = StyleKeyword.None;
            _fallback.Clear();
            _fallback.style.display = DisplayStyle.Flex;
            if (_avatar != null) _fallback.Add(Avatar3DCatalog.Thumbnail(_avatar, _width, _height - 10f));
        }

        void EnsureRig()
        {
            if (_root != null && _texture != null && _texture.IsCreated()) return;
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("Profile Avatar Stage") { hideFlags = HideFlags.DontSave };
            _root.transform.position = Origin;
            _pivot = new GameObject("Pivot");
            _pivot.transform.SetParent(_root.transform, false);
            _model = null;

            if (_texture == null || !_texture.IsCreated())
            {
                if (_texture != null) _texture.Release();
                int w = Mathf.RoundToInt(_width * 2f), h = Mathf.RoundToInt(_height * 2f);
                _texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Profile Avatar",
                    antiAliasing = 4,
                    hideFlags = HideFlags.DontSave
                };
                _texture.Create();
            }

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _camera.cullingMask = 1 << Layer;
            _camera.fieldOfView = 26f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 20f;
            _camera.allowHDR = false;
            _camera.targetTexture = _texture;
            _camera.depth = -50f;
            var data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;
            int renderer = City3DScene.FindUniversalRenderer();
            if (renderer >= 0) data.SetRenderer(renderer);
            cameraObject.transform.localPosition = new Vector3(0f, ModelHeight * 0.52f, 4.4f);
            cameraObject.transform.LookAt(Origin + new Vector3(0f, ModelHeight * 0.5f, 0f));

            AddLight("Key", new Vector3(-1.6f, 2.6f, 2.4f), new Color(1f, 0.96f, 0.9f), 6f);
            _rim = AddLight("Rim", new Vector3(1.4f, 2.2f, -2.2f), new Color(0.6f, 0.75f, 1f), 4f);
        }

        Light AddLight(string name, Vector3 position, Color color, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(_root.transform, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        void SpawnModel(GameObject prefab)
        {
            if (_model != null) Object.Destroy(_model);
            _pivot.transform.localRotation = Quaternion.identity;
            _model = Object.Instantiate(prefab, _pivot.transform, false);
            _model.name = _avatar.id;
            _model.hideFlags = HideFlags.DontSave;
            foreach (var t in _model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            // Анимации и физика из импортированного файла не нужны.
            foreach (var animator in _model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;

            // Нормализуем размер: рост ModelHeight, ступни на нуле, центр на оси вращения.
            var renderers = _model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float height = Mathf.Max(bounds.size.y, 0.001f);
            float scale = ModelHeight / height;
            _model.transform.localScale *= scale;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var pivot = _pivot.transform.position;
            _model.transform.position += new Vector3(pivot.x - bounds.center.x, pivot.y - bounds.min.y, pivot.z - bounds.center.z);
        }

        public void Tick(float dt)
        {
            _time += dt;
            if (_model == null || _pivot == null)
            {
                // Запасной показ: лёгкое покачивание картинки.
                float s = 1f + 0.012f * Mathf.Sin(_time * 1.6f);
                _fallback.style.scale = new Scale(new Vector3(s, s, 1f));
                return;
            }
            if (!_dragging)
            {
                _velocity = Mathf.MoveTowards(_velocity, 0f, 240f * dt);
                _angle += (AutoSpin + _velocity) * dt;
            }
            _pivot.transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
            if (_avatar != null && _rim != null) _rim.color = Color.Lerp(new Color(0.6f, 0.75f, 1f), _avatar.accent, 0.5f);
        }

        /// <summary>Убрать камеру, модель и текстуру (уход со страницы профиля).</summary>
        public void Release()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _model = null;
            _camera = null;
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
            }
            _texture = null;
            style.backgroundImage = StyleKeyword.None;
        }
    }
}
