using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>Optional local-only profile preview. No resource paths from player data.
    /// The original turntable determines layout and remains the fallback.</summary>
    public sealed class ProfileAvatarPreviewStage : VisualElement
    {
        const int Layer = 31;
        public const string MaleResource = "CityHighways/Profile3D/Male";
        public const string FemaleResource = "CityHighways/Profile3D/Female";
        static int nextStage;
        readonly Vector3 origin = new Vector3(-8000f - 30f * (nextStage++ % 128), -8000f, -8000f);
        readonly VisualElement fallback, surface;
        readonly Button motionButton;
        ProfileAvatarMotion motion;
        ProfileAvatarMotionMode motionMode;
        float phase, lastMotionTime;
        readonly Func<string, ProfileAvatarRig> load;
        ProfileAppearance appearance;
        bool debugFallback, attached;
        GameObject root, pivot;
        Camera camera;
        RenderTexture texture;
        float yaw = 25f, zoom = 1f, lastX;
        int pointer = -1;

        public bool IsShowing3D => root != null && texture != null;
        public static string ResourceFor(string gender) =>
            gender == CharacterCatalog.Male ? MaleResource :
            gender == CharacterCatalog.Female ? FemaleResource : null;

        public ProfileAvatarPreviewStage(VisualElement fallback)
            : this(fallback, path => Resources.Load<GameObject>(path)?.GetComponent<ProfileAvatarRig>()) { }

        // Injectable authored prefab source for deterministic tests; production uses fixed paths.
        public ProfileAvatarPreviewStage(VisualElement fallback, Func<string, ProfileAvatarRig> load)
        {
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            this.load = load ?? throw new ArgumentNullException(nameof(load));
            style.flexShrink = 0f;
            style.alignSelf = Align.Center;
            Add(fallback);
            surface = new VisualElement { name = "profile-avatar-3d", focusable = true };
            Ui.Fill(surface);
            surface.style.display = DisplayStyle.None;
            surface.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            Add(surface);
            motionButton = new Button(CycleMotion) { name = "profile-avatar-motion" };
            motionButton.style.position = Position.Absolute;
            motionButton.style.bottom = 4f;
            motionButton.style.right = 4f;
            motionButton.style.display = DisplayStyle.None;
            Add(motionButton);
            UpdateMotionLabel();
            RegisterCallback<AttachToPanelEvent>(_ => { attached = true; Refresh(); });
            RegisterCallback<DetachFromPanelEvent>(_ => { attached = false; Release(); });
            surface.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || !IsShowing3D) return;
                pointer = e.pointerId; lastX = e.position.x;
                surface.CapturePointer(pointer);
                e.StopPropagation();
            });
            surface.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (pointer != e.pointerId) return;
                Rotate(e.position.x - lastX);
                lastX = e.position.x;
                e.StopPropagation();
            });
            surface.RegisterCallback<PointerUpEvent>(e =>
            {
                if (pointer != e.pointerId) return;
                CancelPointer();
                e.StopPropagation();
            });
            surface.RegisterCallback<PointerCaptureOutEvent>(_ => pointer = -1);
            surface.RegisterCallback<WheelEvent>(e =>
            {
                SetZoom(zoom - e.delta.y * .04f);
                e.StopPropagation();
            });
            surface.RegisterCallback<KeyDownEvent>(e =>
            {
                switch (e.keyCode)
                {
                    case KeyCode.LeftArrow: Rotate(-15f); break;
                    case KeyCode.RightArrow: Rotate(15f); break;
                    case KeyCode.UpArrow: SetZoom(zoom + .1f); break;
                    case KeyCode.DownArrow: SetZoom(zoom - .1f); break;
                    case KeyCode.Home: yaw = 25f; zoom = 1f; UpdateCamera(); break;
                    default: return;
                }
                e.StopPropagation();
            });
            schedule.Execute(() =>
            {
                if (camera != null)
                {
                    if (texture == null || !texture.IsCreated()) { Release(); return; }
                    bool visible = attached && ProfileRenderVisibility.IsVisible(this);
                    camera.enabled = visible;
                    motionButton.SetEnabled(!Ui.ReducedMotion);
                    if (Ui.ReducedMotion && motion != null && motionMode != ProfileAvatarMotionMode.Still)
                    {
                        motionMode = ProfileAvatarMotionMode.Still;
                        phase = 0f;
                        motion.SampleValidated(motionMode, phase);
                        UpdateMotionLabel();
                    }
                    float now = Time.realtimeSinceStartup;
                    if (visible && motion != null && motionMode != ProfileAvatarMotionMode.Still)
                    {
                        phase = Mathf.Repeat(phase + Mathf.Clamp(now - lastMotionTime, 0f, .1f) *
                            (motionMode == ProfileAvatarMotionMode.Walk ? 1.1f : .35f), 1f);
                        motion.SampleValidated(motionMode, phase);
                    }
                    lastMotionTime = now;
                }
            }).Every(33);
        }

        public void Show(ProfileAppearance look, bool force2D = false)
        {
            appearance = look;
            debugFallback = force2D;
            Refresh();
        }

        public void SetDebugFallback(bool value)
        {
            if (debugFallback == value) return;
            debugFallback = value;
            Refresh();
        }

        void Refresh()
        {
            Release();
            if (!attached || debugFallback || appearance == null || !Application.isPlaying) return;
            string path = ResourceFor(appearance.Gender);
            if (path == null) return;
            try
            {
                var prefab = load(path);
                if (prefab == null || !prefab.gameObject.activeSelf || !prefab.TryValidate(out _) || prefab.Gender != appearance.Gender) return;
                root = new GameObject("Local profile 3D preview") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false);
                // Separate from the existing role-avatar stages, with a short camera frustum.
                root.transform.position = origin;
                pivot = new GameObject("Pivot");
                pivot.transform.SetParent(root.transform, false);
                var rig = Object.Instantiate(prefab, pivot.transform, false);
                if (!rig.TryApply(appearance, out _)) { Release(); return; }
                foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var animator in rig.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var collider in rig.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in rig.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
                root.SetActive(true);
                // Only selected renderers determine framing; hidden items do not change bounds.
                var renderers = rig.GetComponentsInChildren<Renderer>(true);
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled || !IsSelected(renderer.transform, pivot.transform)) continue;
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                float extent = bounds.size.magnitude;
                if (!hasBounds || float.IsNaN(extent) || float.IsInfinity(extent) || extent < .001f || extent > 10000f) { Release(); return; }
                float scale = 1.8f / extent;
                Vector3 centerOffset = bounds.center - rig.transform.position;
                rig.transform.localScale *= scale;
                rig.transform.position = pivot.transform.position - centerOffset * scale;
                texture = new RenderTexture(460, 690, 24, RenderTextureFormat.ARGB32)
                { name = "Local profile avatar", hideFlags = HideFlags.HideAndDontSave };
                if (!texture.Create()) { Release(); return; }
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform, false);
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.10f, .15f, .20f, 1f);
                camera.orthographic = true;
                camera.aspect = 460f / 690f;
                camera.cullingMask = 1 << Layer;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 12f;
                camera.allowHDR = false;
                camera.targetTexture = texture;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                int rendererIndex = City3DScene.FindUniversalRenderer();
                if (rendererIndex >= 0) data.SetRenderer(rendererIndex);
                AddLight(new Vector3(-2f, 3f, 3f), new Color(1f, .95f, .88f), 5f);
                AddLight(new Vector3(2f, 2f, -3f), new Color(.65f, .8f, 1f), 4f);
                UpdateCamera();
                root.SetActive(true);
                camera.enabled = true;
                surface.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
                surface.style.display = DisplayStyle.Flex;
                fallback.style.opacity = 0f;
                motion = rig.GetComponent<ProfileAvatarMotion>();
                if (motion != null && motion.IsValid())
                {
                    motionButton.style.display = DisplayStyle.Flex;
                    lastMotionTime = Time.realtimeSinceStartup;
                    UpdateMotionLabel();
                }
                else motion = null;
            }
            catch (Exception ex)
            {
                Release();
                Debug.LogWarning("Profile 3D preview fell back to 2D: " + ex.Message);
            }
        }

        void CycleMotion()
        {
            if (motion == null || Ui.ReducedMotion) return;
            motionMode = motionMode == ProfileAvatarMotionMode.Still ? ProfileAvatarMotionMode.Idle :
                motionMode == ProfileAvatarMotionMode.Idle ? ProfileAvatarMotionMode.Walk : ProfileAvatarMotionMode.Still;
            phase = 0f;
            lastMotionTime = Time.realtimeSinceStartup;
            motion.SampleValidated(motionMode, phase);
            UpdateMotionLabel();
        }

        void UpdateMotionLabel()
        {
            bool en = LocalizationService.Language == "en";
            motionButton.text = motionMode == ProfileAvatarMotionMode.Idle ? (en ? "Idle" : "Ожидание") :
                motionMode == ProfileAvatarMotionMode.Walk ? (en ? "Walk" : "Ходьба") : (en ? "Still" : "Статично");
            motionButton.tooltip = en ? "Change preview motion" : "Сменить анимацию предпросмотра";
        }

        static bool IsSelected(Transform child, Transform stop)
        {
            for (var t = child; t != null && t != stop; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return true;
        }

        void AddLight(Vector3 position, Color color, float intensity)
        {
            var obj = new GameObject("Preview light");
            obj.transform.SetParent(root.transform, false);
            obj.transform.localPosition = position;
            var light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.cullingMask = 1 << Layer;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        void Rotate(float delta)
        {
            yaw = Mathf.Repeat(yaw + delta, 360f);
            UpdateCamera();
        }

        void SetZoom(float value)
        {
            zoom = Mathf.Clamp(value, .75f, 1.5f);
            UpdateCamera();
        }

        void UpdateCamera()
        {
            if (pivot == null || camera == null) return;
            pivot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            camera.transform.localPosition = new Vector3(0f, .15f, 4f);
            camera.transform.LookAt(pivot.transform.position);
            camera.orthographicSize = 1.5f / zoom;
        }

        void CancelPointer()
        {
            int captured = pointer;
            pointer = -1;
            if (captured >= 0 && surface.HasPointerCapture(captured)) surface.ReleasePointer(captured);
        }

        public void Release()
        {
            CancelPointer();
            motion = null;
            motionMode = ProfileAvatarMotionMode.Still;
            phase = 0f;
            motionButton.style.display = DisplayStyle.None;
            surface.style.backgroundImage = StyleKeyword.None;
            surface.style.display = DisplayStyle.None;
            fallback.style.opacity = 1f;
            if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
            if (root != null) { root.SetActive(false); DestroyOwned(root); }
            if (texture != null) { texture.Release(); DestroyOwned(texture); }
            camera = null; root = null; pivot = null; texture = null;
        }

        static void DestroyOwned(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
