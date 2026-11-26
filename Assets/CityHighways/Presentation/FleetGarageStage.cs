using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityHighways.Presentation
{
    /// <summary>One isolated camera for the entire garage. Owns only its preview resources.</summary>
    public sealed class FleetGarageStage : VisualElement
    {
        const int Layer = 31;
        readonly VisualElement surface;
        readonly FleetVehiclePreview fallback;
        readonly Button motionButton;
        FleetLivery current;
        FleetVehicleVisual model;
        GameObject root, pivot;
        Camera camera;
        RenderTexture texture;
        Material material;
        bool attached, working;
        float yaw = 35f, zoom = 1f, time, last, lastX;
        int pointer = -1;
        public bool IsShowing3D => camera != null && texture != null && model?.Root != null;

        public FleetGarageStage()
        {
            name = "garage-3d-stage";
            style.alignItems = Align.Center;
            fallback = new FleetVehiclePreview();
            Add(fallback);
            surface = new VisualElement { name = "garage-3d-surface", focusable = true };
            surface.style.width = 300f;
            surface.style.maxWidth = new StyleLength(Length.Percent(100f));
            surface.style.height = 180f;
            surface.style.display = DisplayStyle.None;
            surface.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            Add(surface);
            motionButton = Ui.Button(LocalizationService.Get("profile.garage.motion"), () =>
            {
                working = !working;
                time = 0f;
                model?.ResetMotion();
            }, ButtonKind.Secondary, 13f);
            motionButton.style.display = DisplayStyle.None;
            Add(motionButton);
            RegisterCallback<AttachToPanelEvent>(_ => { attached = true; Rebuild(); });
            RegisterCallback<DetachFromPanelEvent>(_ => { attached = false; Release(); });
            surface.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || !IsShowing3D) return;
                pointer = e.pointerId; lastX = e.position.x;
                surface.CapturePointer(pointer); e.StopPropagation();
            });
            surface.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (pointer != e.pointerId) return;
                yaw = Mathf.Repeat(yaw + e.position.x - lastX, 360f);
                lastX = e.position.x; UpdateView(); e.StopPropagation();
            });
            surface.RegisterCallback<PointerUpEvent>(e =>
            {
                if (pointer != e.pointerId) return;
                CancelPointer(); e.StopPropagation();
            });
            surface.RegisterCallback<PointerCaptureOutEvent>(_ => pointer = -1);
            surface.RegisterCallback<WheelEvent>(e =>
            {
                zoom = Mathf.Clamp(zoom - e.delta.y * .04f, .8f, 1.4f);
                UpdateView(); e.StopPropagation();
            });
            surface.RegisterCallback<KeyDownEvent>(e =>
            {
                switch (e.keyCode)
                {
                    case KeyCode.LeftArrow: yaw -= 15f; break;
                    case KeyCode.RightArrow: yaw += 15f; break;
                    case KeyCode.UpArrow: zoom = Mathf.Min(1.4f, zoom + .1f); break;
                    case KeyCode.DownArrow: zoom = Mathf.Max(.8f, zoom - .1f); break;
                    case KeyCode.Home: yaw = 35f; zoom = 1f; break;
                    default: return;
                }
                yaw = Mathf.Repeat(yaw, 360f);
                UpdateView(); e.StopPropagation();
            });
            schedule.Execute(Tick).Every(33);
        }

        public void Show(FleetLivery value)
        {
            current = value;
            fallback.Set(value);
            if (value != null && model != null && model.Apply(value)) return;
            Rebuild();
        }

        void Rebuild()
        {
            Release();
            if (!attached || !Application.isPlaying || current == null) return;
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null || !shader.isSupported) return;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                root = new GameObject("Garage preview stage") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
                root.transform.position = new Vector3(-8000f,-8100f,-8000f);
                pivot = new GameObject("Vehicle pivot") { layer = Layer };
                pivot.transform.SetParent(root.transform, false);
                model = FleetVehicleVisual.Build(current, material, pivot.transform);
                if (model == null) { Release(); return; }
                Bounds bounds = default;
                bool first = true;
                foreach (var renderer in model.Root.GetComponentsInChildren<Renderer>())
                {
                    if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                float extent = bounds.size.magnitude;
                if (first || extent < .001f || float.IsNaN(extent) || float.IsInfinity(extent))
                { Release(); return; }
                float scale = 2.2f / extent;
                model.Root.transform.localScale = Vector3.one * scale;
                model.Root.transform.localPosition = -(bounds.center - model.Root.transform.position) * scale;
                texture = new RenderTexture(480, 288, 24, RenderTextureFormat.ARGB32)
                    { hideFlags = HideFlags.HideAndDontSave, name = "Garage preview" };
                if (!texture.Create()) { Release(); return; }
                var cameraGo = new GameObject("Garage camera");
                cameraGo.transform.SetParent(root.transform, false);
                camera = cameraGo.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.10f,.14f,.19f,1f);
                camera.orthographic = true;
                camera.aspect = 480f / 288f;
                camera.cullingMask = 1 << Layer;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 12f;
                camera.allowHDR = false;
                camera.targetTexture = texture;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.renderShadows = false;
                int index = City3DScene.FindUniversalRenderer();
                if (index >= 0) data.SetRenderer(index);
                AddLight(new Vector3(-2,3,3), new Color(1f,.95f,.88f), 5f);
                AddLight(new Vector3(2,2,-3), new Color(.65f,.8f,1f), 4f);
                UpdateView();
                surface.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
                surface.style.display = DisplayStyle.Flex;
                fallback.style.display = DisplayStyle.None;
                motionButton.style.display = DisplayStyle.Flex;
                last = Time.realtimeSinceStartup;
                camera.enabled = true;
            }
            catch (Exception ex) { Release(); Debug.LogWarning("Garage preview fallback: " + ex.Message); }
        }

        void AddLight(Vector3 position, Color color, float intensity)
        {
            var obj = new GameObject("Garage light");
            obj.transform.SetParent(root.transform, false);
            obj.transform.localPosition = position;
            var light = obj.AddComponent<Light>();
            light.type = LightType.Point; light.range = 8f;
            light.cullingMask = 1 << Layer; light.color = color; light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        void UpdateView()
        {
            if (camera == null || pivot == null) return;
            pivot.transform.localRotation = Quaternion.Euler(0f,yaw,0f);
            camera.transform.localPosition = new Vector3(0f,1.35f,4f);
            camera.transform.LookAt(pivot.transform.position);
            camera.orthographicSize = 1.45f / zoom;
        }

        void Tick()
        {
            if (camera == null) return;
            bool visible = attached;
            for (VisualElement el = this; el != null && visible; el = el.parent)
                visible = el.resolvedStyle.display != DisplayStyle.None && el.resolvedStyle.visibility == Visibility.Visible;
            camera.enabled = visible;
            float now = Time.realtimeSinceStartup;
            if (visible && working && !Ui.ReducedMotion)
            {
                time = Mathf.Repeat(time + Mathf.Clamp(now-last,0f,.1f), 360f);
                model?.SampleMotion(current.Vehicle == "crane" ? 0f : time * .5f, time, true);
            }
            else model?.ResetMotion();
            last = now;
        }

        void CancelPointer()
        {
            int id = pointer; pointer = -1;
            if (id >= 0 && surface.HasPointerCapture(id)) surface.ReleasePointer(id);
        }

        public void Release()
        {
            CancelPointer();
            working = false; time = 0f;
            surface.style.backgroundImage = StyleKeyword.None;
            surface.style.display = DisplayStyle.None;
            fallback.style.display = DisplayStyle.Flex;
            motionButton.style.display = DisplayStyle.None;
            if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
            if (root != null) { root.SetActive(false); DestroyOwned(root); }
            if (texture != null) { texture.Release(); DestroyOwned(texture); }
            if (material != null) DestroyOwned(material);
            camera = null; root = pivot = null; texture = null; material = null; model = null;
        }

        static void DestroyOwned(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
