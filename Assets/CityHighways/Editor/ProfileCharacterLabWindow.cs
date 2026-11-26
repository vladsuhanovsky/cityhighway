using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Temporary 3D authoring preview. No profile writes or permanent scene objects.</summary>
public sealed class ProfileCharacterLabWindow : EditorWindow
{
    ProfileCharacterLibrary library;
    PreviewRenderUtility preview;
    GameObject model;
    readonly List<Material> materials = new List<Material>();
    Outfit outfit;
    float yaw = 25f;
    bool prototype = true;
    string status = "";
    Vector2 scroll;

    [MenuItem("City Highways/Profile/Character 3D Lab")]
    static void Open() => GetWindow<ProfileCharacterLabWindow>("Character 3D Lab");

    void OnEnable() { outfit = Outfit.Default; }
    void OnDisable() { Release(); }

    void Release()
    {
        if (model != null) DestroyImmediate(model);
        model = null;
        foreach (var material in materials) if (material != null) DestroyImmediate(material);
        materials.Clear();
        preview?.Cleanup();
        preview = null;
    }

    void Refresh()
    {
        Release();
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var look = ProfileCharacterAppearance.FromLook(outfit.Serialize());
            preview = new PreviewRenderUtility();
            if (!prototype && library != null) model = library.InstantiateLook(look);
            else if (prototype) model = BuildPrototype(look);
            if (model == null)
            {
                status = "Missing or invalid 3D bindings for this outfit. The existing 2D presentation remains unchanged.";
                Release();
                return;
            }
            preview.AddSingleGO(model);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.10f, .15f, .20f);
            preview.camera.orthographic = true;
            preview.lights[0].intensity = 1.5f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = .8f;
            status = prototype ? "Procedural silhouette study — placeholder geometry, not production character art."
                : "Authored prefab preview (socket-local visuals only).";
        }
        catch (Exception ex) { status = ex.Message; Release(); }
    }

    void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Release();
            EditorGUILayout.HelpBox("Use the Lab outside Play Mode.", MessageType.Info);
            return;
        }
        if (outfit == null) outfit = Outfit.Default;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Catalog preview: no purchases or profile writes. Library edits require an explicit action.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        prototype = EditorGUILayout.Toggle("Procedural prototype", prototype);
        library = (ProfileCharacterLibrary)EditorGUILayout.ObjectField("3D library", library, typeof(ProfileCharacterLibrary), false);
        if (EditorGUI.EndChangeCheck()) Refresh();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Male defaults")) { outfit = Outfit.DefaultFor(CharacterCatalog.Male); Refresh(); }
        if (GUILayout.Button("Female defaults")) { outfit = Outfit.DefaultFor(CharacterCatalog.Female); Refresh(); }
        EditorGUILayout.EndHorizontal();
        foreach (var slot in CharacterCatalog.Slots)
        {
            var items = CharacterCatalog.ItemsIn(slot, outfit.Gender).ToList();
            var ids = items.Select(i => i.id).ToList();
            var names = items.Select(i => i.DisplayName).ToList();
            if (!CharacterCatalog.IsRequired(slot)) { ids.Insert(0, ""); names.Insert(0, "None"); }
            if (ids.Count == 0) continue;
            int selected = Mathf.Max(0, ids.IndexOf(outfit.Get(slot)));
            int next = EditorGUILayout.Popup(CharacterCatalog.SlotTitle(slot), selected, names.ToArray());
            if (next != selected) { outfit = outfit.With(slot, ids[next]); Refresh(); }
        }
        if (GUILayout.Button("Refresh preview")) Refresh();
        yaw = EditorGUILayout.Slider("Rotation", yaw, -180f, 180f);
        if (preview == null && string.IsNullOrEmpty(status)) Refresh();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        Rect rect = GUILayoutUtility.GetRect(240f, 380f, GUILayout.ExpandWidth(true));
        if (preview != null && model != null && Event.current.type == EventType.Repaint)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float size = Mathf.Max(.1f, bounds.size.magnitude);
                preview.camera.orthographicSize = size * .57f / Mathf.Min(1f, rect.width / Mathf.Max(1f, rect.height));
                preview.camera.transform.position = bounds.center + Quaternion.Euler(8f, yaw, 0f) * new Vector3(0, 0, size * 2f);
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = size * 5f + 1f;
                preview.BeginPreview(rect, GUIStyle.none);
                preview.Render(true);
                GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
            }
        }
        if (library != null)
        {
            if (GUILayout.Button("Add missing catalog binding rows"))
            {
                var rows = (library.items ?? Array.Empty<ProfileCharacterLibrary.ItemBinding>()).ToList();
                var ids = new HashSet<string>(rows.Where(r => r != null).Select(r => r.itemId), StringComparer.Ordinal);
                var missing = CharacterCatalog.Slots.SelectMany(slot => CharacterCatalog.ItemsIn(slot))
                    .Where(i => ids.Add(i.id))
                    .Select(i => new ProfileCharacterLibrary.ItemBinding
                    { itemId = i.id, socket = ProfileCharacterLibrary.SocketFor(i.slot) }).ToArray();
                if (missing.Length > 0)
                {
                    Undo.RecordObject(library, "Add character catalog bindings");
                    rows.AddRange(missing);
                    library.items = rows.ToArray();
                    EditorUtility.SetDirty(library);
                    status = "Added " + missing.Length + " empty binding rows; assign authored prefabs in Inspector.";
                }
            }
            var issues = library.Audit();
            EditorGUILayout.LabelField("Library issues", issues.Length.ToString());
            foreach (string issue in issues) EditorGUILayout.LabelField(issue);
        }
        EditorGUILayout.EndScrollView();
    }

    GameObject BuildPrototype(ProfileCharacterAppearance look)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No preview shader available.");
        var root = new GameObject("Character silhouette study") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var palette = new Dictionary<Color, Material>();
            void Part(string name, PrimitiveType shape, Vector3 pos, Vector3 scale, Color color)
            {
                var part = GameObject.CreatePrimitive(shape);
                part.name = name;
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = pos;
                part.transform.localScale = scale;
                var collider = part.GetComponent<Collider>();
                if (collider != null) DestroyImmediate(collider);
                if (!palette.TryGetValue(color, out var material))
                {
                    material = new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };
                    palette.Add(color, material);
                    materials.Add(material);
                }
                part.GetComponent<Renderer>().sharedMaterial = material;
            }
            Color skin = look.Tone == "dark" ? new Color(.42f,.25f,.16f) :
                look.Tone == "tan" ? new Color(.70f,.46f,.31f) : new Color(.88f,.68f,.51f);
            Color navy = new Color(.13f,.22f,.31f), orange = new Color(.96f,.48f,.12f);
            Color dark = new Color(.12f,.10f,.09f), white = new Color(.91f,.91f,.84f);
            bool female = look.Gender == CharacterCatalog.Female;
            string top = look.Visible("top"), bottom = look.Visible("bottom"), hat = look.Visible("hat"), hair = look.Visible("hair"), acc = look.Visible("accessory");
            Color shirt = top.Contains("vest") ? orange : top.Contains("jacket") || top.Contains("blazer") ? navy :
                top.Contains("sweater") || top.Contains("cardigan") ? new Color(.34f,.48f,.40f) : white;
            Color trousers = bottom.Contains("jeans") ? new Color(.25f,.39f,.58f) :
                bottom.Contains("overalls") ? orange : navy;
            float shoulders = female ? .46f : .56f;
            Part("Torso", PrimitiveType.Capsule, new Vector3(0,1.12f,0), new Vector3(shoulders,.36f,.30f), shirt);
            Part("Hips", PrimitiveType.Sphere, new Vector3(0,.81f,0), new Vector3(.46f,.27f,.30f), trousers);
            Part("Head", PrimitiveType.Sphere, new Vector3(0,1.62f,0), new Vector3(.41f,.46f,.38f), skin);
            Part("Neck", PrimitiveType.Cylinder, new Vector3(0,1.40f,0), new Vector3(.15f,.08f,.15f), skin);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * .13f, arm = side * (shoulders * .5f + .06f);
                Part("Leg", PrimitiveType.Capsule,new Vector3(x,.48f,0),new Vector3(.20f,.34f,.22f),trousers);
                Part("Boot", PrimitiveType.Cube,new Vector3(x,.12f,.045f),new Vector3(.21f,.19f,.32f),
                    look.Visible("shoes").Contains("sneakers") ? white : dark);
                Part("Sleeve",PrimitiveType.Capsule,new Vector3(arm,1.16f,0),new Vector3(.17f,.19f,.19f),shirt);
                Part("Arm",PrimitiveType.Capsule,new Vector3(arm,.94f,0),new Vector3(.14f,.17f,.15f),skin);
                Part("Eye",PrimitiveType.Sphere,new Vector3(side*.076f,1.65f,.173f),new Vector3(.038f,.044f,.025f),dark);
                if (top.Contains("vest"))
                    Part("Reflective stripe",PrimitiveType.Cube,new Vector3(side*.12f,1.15f,.14f),new Vector3(.035f,.36f,.026f),white);
            }
            if (!string.IsNullOrEmpty(hair))
            {
                Part("Hair",PrimitiveType.Sphere,new Vector3(0,1.75f,-.035f),new Vector3(.43f,.25f,.39f),dark);
                if (hair.Contains("long") || hair.Contains("bob") || hair.Contains("curly"))
                    Part("Hair back",PrimitiveType.Sphere,new Vector3(0,1.54f,-.13f),new Vector3(.43f,.43f,.23f),dark);
                if (hair.Contains("bun") || hair.Contains("ponytail"))
                    Part("Hair tie",PrimitiveType.Sphere,new Vector3(0,1.74f,-.22f),new Vector3(.20f,.22f,.20f),dark);
                if (hair.Contains("beard"))
                    Part("Beard",PrimitiveType.Sphere,new Vector3(0,1.50f,.12f),new Vector3(.28f,.15f,.20f),dark);
            }
            if (!string.IsNullOrEmpty(hat))
            {
                Color hatColor = hat.Contains("hardhat") ? orange : dark;
                Part("Hat crown",PrimitiveType.Sphere,new Vector3(0,1.81f,0),new Vector3(.46f,.22f,.43f),hatColor);
                if (hat.Contains("tophat")) Part("Tall crown",PrimitiveType.Cylinder,new Vector3(0,1.94f,0),new Vector3(.36f,.16f,.36f),hatColor);
                Part("Hat brim",PrimitiveType.Cylinder,new Vector3(0,1.76f,.035f),new Vector3(.52f,.024f,.51f),hatColor);
            }
            if (acc.Contains("glasses"))
                foreach (int side in new[] {-1,1})
                    Part("Glasses",PrimitiveType.Cube,new Vector3(side*.087f,1.65f,.195f),new Vector3(.12f,.065f,.025f),navy);
            else if (!string.IsNullOrEmpty(acc))
                Part("Accessory marker",PrimitiveType.Cube,new Vector3(shoulders*.5f+.12f,.87f,.11f),new Vector3(.12f,.19f,.08f),navy);
            return root;
        }
        catch { DestroyImmediate(root); throw; }
    }
}
