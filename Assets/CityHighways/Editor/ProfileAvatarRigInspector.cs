using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ProfileAvatarRig))]
public sealed class ProfileAvatarRigInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var rig = (ProfileAvatarRig)target;
        EditorGUILayout.HelpBox("Assign the existing wardrobe item ID to each separate visual variant under the shared rig. Do not nest variant roots or use them as skeleton parents. No runtime Resources path is derived from the profile.", MessageType.Info);
        if (!rig.TryValidate(out string error))
        {
            EditorGUILayout.HelpBox(error, MessageType.Warning);
            return;
        }
        string[] missing = rig.MissingCatalogIds();
        if (missing.Length == 0)
            EditorGUILayout.HelpBox("All catalog IDs for this gender have a visual binding. Verify actual fit, animation and licensing separately.", MessageType.Info);
        else
            EditorGUILayout.HelpBox("Missing catalog bindings (" + missing.Length + "): " + string.Join(", ", missing), MessageType.Warning);
        if (!rig.gameObject.scene.IsValid() || !rig.gameObject.scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (GUILayout.Button("Preview default outfit on scene instance"))
        {
            var descendants = rig.GetComponentsInChildren<Transform>(true);
            var objects = new Object[descendants.Length];
            for (int i = 0; i < descendants.Length; i++) objects[i] = descendants[i].gameObject;
            Undo.RecordObjects(objects, "Preview profile avatar outfit");
            if (!rig.TryApply(ProfileAppearance.FromOutfit(Outfit.DefaultFor(rig.Gender)), out error))
                Debug.LogWarning(error, rig);
        }
    }
}
