using System;
using System.Collections.Generic;
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
        public const string MaleArtResource = "CityHighways/Profile3DArt/Male";
        public const string FemaleArtResource = "CityHighways/Profile3DArt/Female";
        public const string MaleHumanoidResource = "CityHighways/Profile3DProfessional/Male";
        public const string FemaleHumanoidResource = "CityHighways/Profile3DProfessional/Female";
        static int nextStage;
        readonly Vector3 origin = new Vector3(-8000f - 30f * (nextStage++ % 128), -8000f, -8000f);
        readonly VisualElement fallback, surface;
        readonly Button motionButton;
        ProfileAvatarMotion motion;
        ProfileHumanoidPreviewPlayer humanoid;
#if UNITY_EDITOR
        readonly Button trialButton;
        bool trialRequested;
        ProfileRoadWorkerPreviewPlayer trialPlayer;
#endif
        ProfileAvatarMotionMode motionMode;
        float phase, lastMotionTime;
        readonly Func<string, ProfileAvatarRig> load;
        ProfileAppearance appearance;
        bool debugFallback, attached;
        GameObject root, pivot;
        Camera camera;
        RenderTexture texture;
        readonly List<Material> stageMaterials = new List<Material>(3);
        float yaw = 25f, zoom = 1f, lastX;
        int pointer = -1;

        public bool IsShowing3D => root != null && texture != null;
        public static string ResourceFor(string gender) =>
            gender == CharacterCatalog.Male ? MaleResource :
            gender == CharacterCatalog.Female ? FemaleResource : null;

        public static string ArtResourceFor(string gender) =>
            gender == CharacterCatalog.Male ? MaleArtResource :
            gender == CharacterCatalog.Female ? FemaleArtResource : null;

        public static string HumanoidResourceFor(string gender) =>
            gender == CharacterCatalog.Male ? MaleHumanoidResource :
            gender == CharacterCatalog.Female ? FemaleHumanoidResource : null;

        public static ProfileAvatarRig SelectPreviewRig(string gender, Func<string, ProfileAvatarRig> source)
        {
            if (source == null || ResourceFor(gender) == null) return null;
            bool Valid(ProfileAvatarRig rig) => rig != null && rig.gameObject.activeSelf &&
                rig.Gender == gender && rig.TryValidate(out _) && rig.MissingCatalogIds().Length == 0;
            var professional = source(HumanoidResourceFor(gender));
            if (Valid(professional))
            {
                var contract = professional.GetComponent<ProfileHumanoidArt>();
                if (contract != null && contract.TryValidate(out _)) return professional;
            }
            var art = source(ArtResourceFor(gender));
            if (Valid(art)) return art;
            var legacy = source(ResourceFor(gender));
            return Valid(legacy) ? legacy : null;
        }

        public ProfileAvatarPreviewStage(VisualElement fallback)
            : this(fallback, path => SelectPreviewRig(path == MaleResource ? CharacterCatalog.Male :
                path == FemaleResource ? CharacterCatalog.Female : null,
                resource => Resources.Load<GameObject>(resource)?.GetComponent<ProfileAvatarRig>())) { }

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
#if UNITY_EDITOR
            trialButton = new Button(() => { trialRequested = !trialRequested; Refresh(); })
                { name = "profile-road-worker-v7-demo" };
            trialButton.style.position = Position.Absolute;
            trialButton.style.top = 4f;
            trialButton.style.left = 4f;
            trialButton.style.display = DisplayStyle.None;
            Add(trialButton);
#endif
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
                    if (Ui.ReducedMotion && HasMotion && motionMode != ProfileAvatarMotionMode.Still)
                    {
                        motionMode = ProfileAvatarMotionMode.Still;
                        phase = 0f;
                        SampleMotion();
                        UpdateMotionLabel();
                    }
                    float now = Time.realtimeSinceStartup;
                    if (visible && HasMotion && motionMode != ProfileAvatarMotionMode.Still)
                    {
                        phase = Mathf.Repeat(phase + Mathf.Clamp(now - lastMotionTime, 0f, .1f) *
                            MotionRate(), 1f);
                        SampleMotion();
                    }
                    lastMotionTime = now;
                }
            }).Every(33);
        }

        public void Show(ProfileAppearance look, bool force2D = false)
        {
#if UNITY_EDITOR
            // Selecting a different actual outfit must exit the fixed-look demo.
            if (appearance?.Serialized != look?.Serialized || force2D) trialRequested = false;
#endif
            appearance = look;
            debugFallback = force2D;
            Refresh();
        }

        public void SetDebugFallback(bool value)
        {
            if (debugFallback == value) return;
            debugFallback = value;
#if UNITY_EDITOR
            if (value) trialRequested = false;
#endif
            Refresh();
        }

        void Refresh()
        {
            Release();
#if UNITY_EDITOR
            trialButton.style.display = DisplayStyle.None;
#endif
            if (!attached || debugFallback || appearance == null || !Application.isPlaying) return;
            string path = ResourceFor(appearance.Gender);
            if (path == null) return;
            try
            {
                ProfileAvatarRig prefab = null;
#if UNITY_EDITOR
                GameObject trialAsset = null;
                AnimationClip trialIdle = null, trialWalk = null;
                // Check local availability only while attached in Play Mode.
                var localPath = ProfileRoadWorkerTrial.AssetPath(appearance.Gender == CharacterCatalog.Female, false);
                bool localAvailable = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(localPath) != null;
                trialButton.style.display = localAvailable ? DisplayStyle.Flex : DisplayStyle.None;
                if (trialRequested && (!localAvailable ||
                    !ProfileRoadWorkerPreviewPlayer.TryLoad(appearance.Gender, out trialAsset, out trialIdle, out trialWalk, out _)))
                {
                    trialRequested = false;
                    Debug.LogWarning("Road Worker V7 demo unavailable: validate local GLB and Idle/Walk in Road Worker Trial.");
                }
                UpdateTrialLabel();
                if (!trialRequested)
#endif
                {
                    prefab = load(path);
                    if (prefab == null || !prefab.gameObject.activeSelf || !prefab.TryValidate(out _) || prefab.Gender != appearance.Gender) return;
                }
                root = new GameObject("Local profile 3D preview") { hideFlags = HideFlags.HideAndDontSave };
                root.SetActive(false);
                // Separate from the existing role-avatar stages, with a short camera frustum.
                root.transform.position = origin;
                pivot = new GameObject("Pivot");
                pivot.transform.SetParent(root.transform, false);
                ProfileAvatarRig rig = null;
                GameObject model;
                Transform framing;
#if UNITY_EDITOR
                if (trialRequested)
                {
                    // Animation can target the imported root; framing must live outside it.
                    framing = new GameObject("Local demo framing").transform;
                    framing.SetParent(pivot.transform, false);
                    model = Object.Instantiate(trialAsset, framing, false);
                    trialPlayer = new ProfileRoadWorkerPreviewPlayer(model, trialIdle, trialWalk);
                }
                else
#endif
                {
                    rig = Object.Instantiate(prefab, pivot.transform, false);
                    if (!rig.TryApply(appearance, out _)) { Release(); return; }
                    model = rig.gameObject;
                    framing = rig.transform;
                }
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
                root.SetActive(true);
                var authored = rig == null ? null : rig.GetComponent<ProfileHumanoidArt>();
                if (authored != null && authored.TryValidate(out _))
                    humanoid = new ProfileHumanoidPreviewPlayer(authored);
                // Only selected renderers determine framing; hidden items do not change bounds.
                var renderers = model.GetComponentsInChildren<Renderer>(true);
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
                float scale = 2.12f / extent;
                Vector3 centerOffset = bounds.center - framing.position;
                framing.localScale *= scale;
                framing.position = pivot.transform.position - centerOffset * scale;
                BuildPodium(-bounds.extents.y * scale);
                texture = new RenderTexture(460, 690, 24, RenderTextureFormat.ARGB32)
                { name = "Local profile avatar", hideFlags = HideFlags.HideAndDontSave };
                if (!texture.Create()) { Release(); return; }
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform, false);
                camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.045f, .075f, .115f, 1f);
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
                AddLight(new Vector3(-2f, 3f, 3f), new Color(1f, .91f, .79f), 5.5f);
                AddLight(new Vector3(2f, 2f, -3f), new Color(.52f, .73f, 1f), 4f);
                AddLight(new Vector3(1.7f, .9f, 2.4f), new Color(.78f, .87f, 1f), 1.8f);
                UpdateCamera();
                root.SetActive(true);
                camera.enabled = true;
                surface.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
                surface.style.display = DisplayStyle.Flex;
                fallback.style.opacity = 0f;
                motion = rig == null ? null : rig.GetComponent<ProfileAvatarMotion>();
                if (motion != null && !motion.IsValid()) motion = null;
                if (HasMotion)
                {
                    motionButton.style.display = DisplayStyle.Flex;
                    lastMotionTime = Time.realtimeSinceStartup;
                    UpdateMotionLabel();
                }
            }
            catch (Exception ex)
            {
                Release();
#if UNITY_EDITOR
                trialRequested = false;
                UpdateTrialLabel();
#endif
                Debug.LogWarning("Profile 3D preview fell back to 2D: " + ex.Message);
            }
        }

        bool HasMotion =>
#if UNITY_EDITOR
            trialPlayer != null ||
#endif
            humanoid != null || motion != null;

        float MotionRate()
        {
#if UNITY_EDITOR
            if (trialPlayer != null) return trialPlayer.Rate(motionMode);
#endif
            return motionMode == ProfileAvatarMotionMode.Walk ? 1.1f : .35f;
        }

#if UNITY_EDITOR
        void UpdateTrialLabel()
        {
            bool en = LocalizationService.Language == "en";
            trialButton.text = trialRequested ? (en ? "V7 demo: fixed outfit (exit)" : "Демо V7: комплект (выход)") :
                (en ? "V7 demo" : "Демо V7");
            trialButton.tooltip = en ? "Editor only. Fixed outfit, not equipped or purchased." :
                "Только редактор. Фиксированный комплект, не покупка и не примерка.";
        }
#endif

        void SampleMotion()
        {
#if UNITY_EDITOR
            if (trialPlayer != null) { trialPlayer.Sample(motionMode, phase); return; }
#endif
            if (humanoid != null) humanoid.Sample(motionMode,phase);
            else motion?.SampleValidated(motionMode,phase);
        }

        void CycleMotion()
        {
            if (!HasMotion || Ui.ReducedMotion) return;
            motionMode = motionMode == ProfileAvatarMotionMode.Still ? ProfileAvatarMotionMode.Idle :
                motionMode == ProfileAvatarMotionMode.Idle ? ProfileAvatarMotionMode.Walk : ProfileAvatarMotionMode.Still;
            phase = 0f;
            lastMotionTime = Time.realtimeSinceStartup;
            SampleMotion();
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

        void BuildPodium(float feet)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null || !shader.isSupported) return;
            void Disc(string name, float y, float diameter, float height, Color color)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                obj.name = name;
                obj.layer = Layer;
                obj.transform.SetParent(root.transform, false);
                obj.transform.localPosition = new Vector3(0f, y, 0f);
                obj.transform.localScale = new Vector3(diameter, height * .5f, diameter);
                var collider = obj.GetComponent<Collider>();
                if (collider != null) { collider.enabled = false; DestroyOwned(collider); }
                var material = new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };
                stageMaterials.Add(material);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .32f);
                var renderer = obj.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Disc("Exhibition base", feet - .105f, 1.24f, .17f, new Color(.09f,.14f,.20f));
            Disc("Amber trim", feet - .025f, 1.25f, .028f, new Color(.93f,.57f,.18f));
            Disc("Exhibition top", feet - .007f, 1.19f, .025f, new Color(.19f,.26f,.33f));
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
            camera.transform.localPosition = new Vector3(0f, .39f, 4f);
            camera.transform.LookAt(pivot.transform.position - Vector3.up * .09f);
            camera.orthographicSize = 1.43f / zoom;
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
            humanoid?.Dispose();
            humanoid = null;
#if UNITY_EDITOR
            trialPlayer = null;
#endif
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
            foreach (var material in stageMaterials) if (material != null) DestroyOwned(material);
            stageMaterials.Clear();
            camera = null; root = null; pivot = null; texture = null;
        }

        static void DestroyOwned(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
