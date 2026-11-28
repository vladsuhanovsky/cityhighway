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
