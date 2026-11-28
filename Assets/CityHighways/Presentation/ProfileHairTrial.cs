#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Local diagnostic variants on the same imported V11 skeleton.
    /// No inventory changes, asset writes, production resource lookup or bone toggles.</summary>
    public static class ProfileHairTrial
    {
        public static string AssetPath(bool female) =>
            "Assets/CityHighwaysLicensedArt/RoadWorkerV11/" +
            (female ? "F01" : "M02") + "_V11_HairTrial.glb";

        public static string AutoAssetPath(bool female) =>
            "Assets/CityHighwaysLicensedArt/RoadWorkerV13/" +
            (female ? "F01" : "M02") + "_V13_AutoHair.glb";

        // Resolve every normal/fitted pair and helmet before any change, including
        // a helmet toggle. Imported skeleton and source assets are never mutated.
        public const string FaceAssetPath = "Assets/CityHighwaysLicensedArt/RoadWorkerV14/M02_V14_FaceTrial.glb";

        public static bool TrySelectWithHat(GameObject root, bool female, string id, bool hat, out string error) =>
            TrySelectPaired(root, female, id, hat, null, out error);

        // This is a material experiment on M02, not a catalog item or a new saved slot.
        public static bool TrySelectFaceWithHat(GameObject root, string id, bool hat, bool reducedBeard, out string error) =>
            TrySelectPaired(root, false, id, hat, reducedBeard, out error);

        static bool TrySelectPaired(GameObject root, bool female, string id, bool hat, bool? reducedBeard, out string error)
        {
            error = null;
            var ids = ItemIds(female);
            if (root == null || !ids.Contains(id))
            { error = "Unsupported V13 hair variant."; return false; }
            var character = female ? "F01" : "M02";
            var prefix = character + "_V13_";
            var nodes = root.GetComponentsInChildren<Transform>(true);
            var variants = new Dictionary<string, Renderer[]>(StringComparer.Ordinal);
            var used = new HashSet<Renderer>();
            foreach (var item in ids)
            {
                var catalog = CharacterCatalog.Item(item);
                if (catalog == null || catalog.slot != "hair" ||
                    !catalog.Fits(female ? CharacterCatalog.Female : CharacterCatalog.Male))
                { error = "Invalid hair catalog item: " + item; return false; }
                foreach (var suffix in new[] {"", "_Hat"})
                {
                    var key = item + suffix;
                    if (!TryGroup(nodes, prefix + key, used, out var renderers, out error)) return false;
                    variants.Add(key, renderers);
                }
            }
            if (!TryGroup(nodes, character + "_hardhat_BOUND_TRIAL", used, out var helmet, out error))
                return false;
            Renderer[] originalFace = null, reducedFace = null;
            if (reducedBeard.HasValue)
            {
                if (!TryGroup(nodes, "M02_V10_HeadFace", used, out originalFace, out error) ||
                    !TryGroup(nodes, "M02_V14_FaceReducedBeard", used, out reducedFace, out error))
                    return false;
            }
            var selected = id + (hat ? "_Hat" : "");
            foreach (var pair in variants)
                foreach (var renderer in pair.Value) renderer.enabled = pair.Key == selected;
            foreach (var renderer in helmet) renderer.enabled = hat;
            if (reducedBeard.HasValue)
            {
                foreach (var renderer in originalFace) renderer.enabled = !reducedBeard.Value;
                foreach (var renderer in reducedFace) renderer.enabled = reducedBeard.Value;
            }
            return true;
        }

        static bool TryGroup(Transform[] nodes, string name, HashSet<Renderer> used,
            out Renderer[] renderers, out string error)
        {
            renderers = Array.Empty<Renderer>(); error = null;
            var matches = nodes.Where(n => n.name == name).ToArray();
            if (matches.Length != 1)
            { error = "Missing or duplicate visual group: " + name; return false; }
            renderers = matches[0].GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || renderers.Any(r => !used.Add(r)))
            { error = "Empty or overlapping visual group: " + name; return false; }
            return true;
        }

        public static string[] ItemIds(bool female) => female
            ? new[] { "hair_f_bob", "hair_f_long", "hair_f_ponytail", "hair_f_curly" }
            : new[] { "hair_short", "hair_long", "hair_bun" };

        public static bool TrySelect(GameObject root, bool female, string id, out string error)
        {
            error = null;
            var ids = ItemIds(female);
            if (root == null || !ids.Contains(id))
            { error = "Unsupported V11 hair variant."; return false; }
            var prefix = (female ? "F01" : "M02") + "_V11_";
            var nodes = root.GetComponentsInChildren<Transform>(true);
            var variants = new Dictionary<string, Renderer[]>(StringComparer.Ordinal);
            var used = new HashSet<Renderer>();
            foreach (var item in ids)
            {
                var catalog = CharacterCatalog.Item(item);
                var matches = nodes.Where(n => n.name == prefix + item).ToArray();
                if (catalog == null || catalog.slot != "hair" ||
                    !catalog.Fits(female ? CharacterCatalog.Female : CharacterCatalog.Male) ||
                    matches.Length != 1)
                { error = "Missing, duplicate or invalid hair module: " + item; return false; }
                var renderers = matches[0].GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0 || renderers.Any(r => !used.Add(r)))
                { error = "Empty or overlapping hair modules: " + item; return false; }
                variants.Add(item, renderers);
            }
            // No mutation until the entire set has passed validation.
            foreach (var pair in variants)
                foreach (var renderer in pair.Value)
                    renderer.enabled = pair.Key == id;
            return true;
        }

        public static void SetHardhatVisible(GameObject root, bool female, bool visible)
        {
            if (root == null) return;
            var name = (female ? "F01" : "M02") + "_hardhat_BOUND_TRIAL";
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node.name == name)
                    foreach (var renderer in node.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = visible;
        }
    }
}
#endif
