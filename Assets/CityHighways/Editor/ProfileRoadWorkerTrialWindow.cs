using System;
using System.Linq;
using System.IO;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Local, opt-in, Editor-only test bench. Never installs the fixed outfit
/// in Resources or alters live profile, ownership, saves, or active game scenes.</summary>
public sealed class ProfileRoadWorkerTrialWindow : EditorWindow
{
    bool female, fbx, playing, loaded;
    float phase, yaw = 180f, zoom = 1f;
    double lastTick;
    int selectedClip;
    string status = "";
    ProfileRoadWorkerTrial.Inspection inspection;
    AnimationClip[] clips = Array.Empty<AnimationClip>();
    PreviewRenderUtility preview;
    GameObject instance;
    Animator animator;
    Transform[] nodes;
    Vector3[] positions, scales;
    Quaternion[] rotations;
    Bounds bounds;
    PlayableGraph graph;
    AnimationClipPlayable clipPlayable;
    AnimationClip graphClip;

    [MenuItem("City Highways/Profile/Road Worker V4 Trial")]
    static void Open() => GetWindow<ProfileRoadWorkerTrialWindow>("Road Worker V4");

    void OnDisable() => Release();

    void Update()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (loaded) Release();
            return;
        }
        if (!loaded || !playing || selectedClip == 0) return;
        double now = EditorApplication.timeSinceStartup;
        if (now - lastTick < 1d / 30d) return;
        float dt = Mathf.Clamp((float)(now - lastTick), 0f, .1f);
        lastTick = now;
        phase = Mathf.Repeat(phase + dt / clips[selectedClip - 1].length, 1f);
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Local fixed-outfit art trial only. GLB uses imported transform curves; a valid Unity Humanoid avatar and all wardrobe variants are separate acceptance gates.", MessageType.Info);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Exit Play Mode to use this Editor-only preview.", MessageType.Warning);
            return;
        }
        EditorGUI.BeginChangeCheck();
        female = EditorGUILayout.Popup("Character", female ? 1 : 0, new[] {"M02", "F01"}) == 1;
        fbx = EditorGUILayout.Popup("Format", fbx ? 1 : 0, new[] {"GLB (direct)", "FBX (Humanoid trial)"}) == 1;
        if (EditorGUI.EndChangeCheck()) { Release(); status = ""; }
        string path = ProfileRoadWorkerTrial.AssetPath(female, fbx);
        EditorGUILayout.LabelField("Local asset", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(path, EditorStyles.wordWrappedLabel, GUILayout.Height(35));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Load / Reload")) Load(path);
            if (GUILayout.Button("Release")) { Release(); status = ""; }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Choose local file...")) ChooseFile(path);
            if (GUILayout.Button("Reimport")) Reimport(path);
        }
        if (GUILayout.Button("Show asset in Project"))
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
            else status = "Asset not present in the Unity Asset Database. Choose a file or Reimport.";
        }
        if (fbx && GUILayout.Button("Configure local FBX as Humanoid")) ConfigureFbx(path);
        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, loaded ? MessageType.Warning : MessageType.Info);
        if (!loaded) return;
        EditorGUILayout.LabelField($"Skins: {inspection.Skins}  |  bones: {inspection.Bones}  |  triangles: {inspection.Triangles:N0}");
        EditorGUILayout.LabelField("Valid Humanoid Avatar: " + (inspection.ValidHumanoid ? "yes" : "no / not verified"));
        var labels = new[] {"Rest pose"}.Concat(clips.Select(c => c.name +
            (c.humanMotion ? " [Humanoid]" : c.legacy ? " [Legacy]" : " [Generic]"))).ToArray();
        int next = EditorGUILayout.Popup("Animation", selectedClip, labels);
        if (next != selectedClip) { StopGraph(); selectedClip = next; phase = 0f; playing = false; }
        using (new EditorGUI.DisabledScope(selectedClip == 0))
        {
            bool play = GUILayout.Toggle(playing, "Play loop", "Button");
            if (play != playing) { playing = play; lastTick = EditorApplication.timeSinceStartup; }
            EditorGUI.BeginChangeCheck();
            phase = EditorGUILayout.Slider("Normalized time", phase, 0f, 1f);
            if (EditorGUI.EndChangeCheck()) playing = false;
        }
        yaw = EditorGUILayout.Slider("Orbit", yaw, 0f, 360f);
        zoom = EditorGUILayout.Slider("Zoom", zoom, .7f, 2f);
        Rect rect = GUILayoutUtility.GetRect(100f, Mathf.Clamp(position.height - 310f, 180f, 900f), GUILayout.ExpandWidth(true));
        if (Event.current.type != EventType.Repaint || rect.width < 10 || rect.height < 10) return;
        try { Sample(); Draw(rect); }
        catch (Exception ex) { Release(); status = "Preview failed: " + ex.Message; Repaint(); }
    }

    void Load(string path)
    {
        Release(); status = "";
        string diskPath = FullPath(path);
        if (!File.Exists(diskPath))
        {
            status = "File not found on disk: " + diskPath +
                "\nUse Choose local file... to install the extracted " + (fbx ? "FBX" : "GLB") + ".";
            return;
        }
        if (!fbx && !ProfileRoadWorkerTrial.TryCheckGlbFile(diskPath, out var fileError))
        { status = fileError; return; }
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            var importer = AssetImporter.GetAtPath(path);
            status = "File exists but Unity has not imported a GameObject. " +
                "Importer: " + (importer == null ? "none" : importer.GetType().Name) +
                "; main asset: " + (main == null ? "none" : main.GetType().Name) +
                ". Use Reimport; check Console and the GLB Inspector for import errors. " +
                "Confirm glTFast is installed in Package Manager.";
            return;
        }
        if (!ProfileRoadWorkerTrial.TryInspect(asset, out inspection, out var error))
        { status = "Imported model failed validation: " + error; return; }
        try
        {
            clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => c.length > .001f && !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderBy(c => c.name, StringComparer.Ordinal).ToArray();
            preview = new PreviewRenderUtility();
            instance = preview.InstantiatePrefabInScene(asset);
            if (instance == null) throw new InvalidOperationException("Could not instantiate imported model.");
            foreach (var a in instance.GetComponentsInChildren<Animation>(true)) a.enabled = false;
            animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = false;
            }
            foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
            nodes = instance.GetComponentsInChildren<Transform>(true);
            positions = nodes.Select(n => n.localPosition).ToArray();
            rotations = nodes.Select(n => n.localRotation).ToArray();
            scales = nodes.Select(n => n.localScale).ToArray();
            var renderers = instance.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) throw new InvalidOperationException("No visible renderers.");
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (!(bounds.size.y > .001f) || float.IsInfinity(bounds.size.magnitude))
                throw new InvalidOperationException("Invalid rest bounds.");
            selectedClip = 0; phase = 0f; playing = false; loaded = true;
            status = clips.Length == 0 ? "No imported animation clips. Check the importer's animation setting." :
                "Inspect clothing intersections, fingers, feet and cycle seams. This is not a production prefab.";
        }
        catch (Exception ex) { Release(); status = "Import preview failed: " + ex.Message; }
        Repaint();
    }

    static string FullPath(string assetPath) =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

    void ChooseFile(string path)
    {
        string source = EditorUtility.OpenFilePanel("Select the approved " + (female ? "F01" : "M02") + " model",
            "", fbx ? "fbx" : "glb");
        if (string.IsNullOrEmpty(source)) return;
        if (!string.Equals(Path.GetExtension(source), fbx ? ".fbx" : ".glb", StringComparison.OrdinalIgnoreCase))
        { status = "Wrong file extension for the selected format."; return; }
        if (!fbx && !ProfileRoadWorkerTrial.TryCheckGlbFile(source, out var fileError))
        { status = fileError; return; }
        string destination = FullPath(path);
        if (string.Equals(Path.GetFullPath(source), destination,
            Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        { Reimport(path); return; }
        if (File.Exists(destination))
        { status = "A local model already exists. To protect it, remove or move it manually before choosing a replacement."; return; }
        if (!EditorUtility.DisplayDialog("Install local trial model",
            "Copy " + Path.GetFileName(source) + " to " + path +
            "? Confirm the file matches the selected character. The original is not modified.", "Copy and import", "Cancel")) return;
        Release();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, false);
            Reimport(path);
        }
        catch (Exception ex) { status = "Local copy failed: " + ex.Message; }
    }

    void Reimport(string path)
    {
        Release();
        if (!File.Exists(FullPath(path))) { Load(path); return; }
        try
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Load(path);
        }
        catch (Exception ex) { status = "Unity import failed: " + ex.Message; }
    }

    void ResetPose()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].localPosition = positions[i];
            nodes[i].localRotation = rotations[i];
            nodes[i].localScale = scales[i];
        }
    }

    void Sample()
    {
        if (selectedClip == 0) { StopGraph(); ResetPose(); return; }
        var clip = clips[selectedClip - 1];
        float t = Mathf.Clamp01(phase) * clip.length;
        if (clip.humanMotion)
        {
            if (animator == null || !inspection.ValidHumanoid)
                throw new InvalidOperationException("Humanoid clip requires a valid imported Avatar.");
            if (graphClip != clip || !graph.IsValid())
            {
                StopGraph(); ResetPose();
                animator.enabled = true;
                graph = PlayableGraph.Create("Road Worker local trial");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                clipPlayable = AnimationClipPlayable.Create(graph, clip);
                clipPlayable.SetApplyFootIK(false); clipPlayable.SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Trial", animator);
                output.SetSourcePlayable(clipPlayable);
                graph.Play(); graphClip = clip;
            }
            clipPlayable.SetTime(t);
            graph.Evaluate(0f);
        }
        else
        {
            StopGraph(); ResetPose();
            // The imported clip's bindings must resolve on this imported scene root.
            var curves = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)).ToArray();
            if (curves.Length == 0 || curves.Any(b => !string.IsNullOrEmpty(b.path) && instance.transform.Find(b.path) == null))
                throw new InvalidOperationException("Transform curves do not resolve on the imported root.");
            clip.SampleAnimation(instance, t);
        }
    }

    void Draw(Rect rect)
    {
        var camera = preview.camera;
        float aspect = rect.width / rect.height;
        float radius = bounds.extents.magnitude;
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.y * 1.25f, radius / Mathf.Max(.2f, aspect) * 1.12f) / zoom;
        camera.nearClipPlane = .01f; camera.farClipPlane = Mathf.Max(10f, radius * 10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.09f, .12f, .17f);
        camera.transform.position = bounds.center + Quaternion.Euler(8f, yaw, 0f) * Vector3.back * Mathf.Max(2f, radius * 3f);
        camera.transform.LookAt(bounds.center);
        preview.lights[0].intensity = 1.2f;
        preview.lights[0].transform.rotation = Quaternion.Euler(35f, 30f, 0f);
        preview.lights[1].intensity = .8f;
        preview.lights[1].transform.rotation = Quaternion.Euler(20f, 210f, 0f);
        preview.ambientColor = new Color(.45f, .45f, .45f);
        preview.BeginPreview(rect, GUIStyle.none);
        try { preview.Render(true, false); }
        finally { preview.EndAndDrawPreview(rect); }
    }

    void ConfigureFbx(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) { status = "No local FBX at the displayed path."; return; }
        if (!EditorUtility.DisplayDialog("Local FBX only",
            "Set this local FBX to Humanoid, Create From This Model, import animations and retain bone transforms? Check the mapping and clips in the Inspector after reimport.", "Configure", "Cancel")) return;
        Release();
        try
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.SaveAndReimport();
            Load(path);
        }
        catch (Exception ex) { status = "FBX configuration failed: " + ex.Message; }
    }

    void StopGraph()
    {
        if (graph.IsValid()) graph.Destroy();
        graphClip = null;
        if (animator != null) animator.enabled = false;
    }

    void Release()
    {
        playing = false; loaded = false;
        StopGraph();
        if (instance != null) DestroyImmediate(instance);
        instance = null; animator = null;
        if (preview != null) { preview.Cleanup(); preview = null; }
        nodes = null; positions = null; rotations = null; scales = null;
        clips = Array.Empty<AnimationClip>(); selectedClip = 0; phase = 0f;
    }
}
