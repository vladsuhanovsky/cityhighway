using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    [Flags]
    public enum ProfileBodyRegion
    {
        None = 0, Scalp = 1 << 0, Torso = 1 << 1, UpperArms = 1 << 2,
        Forearms = 1 << 3, Pelvis = 1 << 4, Thighs = 1 << 5,
        Calves = 1 << 6, Feet = 1 << 7, All = (1 << 8) - 1
    }

    /// <summary>Optional authored masks for segmented body renderers. Only the
    /// visible catalog items contribute coverage; no changes to ownership, bones,
    /// material assets or source meshes. One renderer must represent one region.</summary>
    [DisallowMultipleComponent]
    public sealed class ProfileBodyCoverage : MonoBehaviour
    {
        [Serializable]
        public sealed class RegionBinding
        {
            public string bodyId;
            public ProfileBodyRegion region;
            public SkinnedMeshRenderer renderer;
        }

        [Serializable]
        public sealed class ItemCoverage
        {
            public string itemId;
            public ProfileBodyRegion hides;
        }

        [SerializeField] RegionBinding[] regions = Array.Empty<RegionBinding>();
        [SerializeField] ItemCoverage[] items = Array.Empty<ItemCoverage>();

        internal bool TryValidate(Dictionary<string, GameObject> variants, out string error)
        {
            error = null;
            if (regions == null || items == null || regions.Length == 0)
            { error = "Body coverage requires authored region renderers."; return false; }
            var bodyRegions = new Dictionary<string, ProfileBodyRegion>(StringComparer.Ordinal);
            var renderers = new HashSet<SkinnedMeshRenderer>();
            foreach (var binding in regions)
            {
                var body = CharacterCatalog.Item(binding?.bodyId);
                if (body == null || body.slot != "body" || !variants.TryGetValue(body.id, out var root) ||
                    binding.renderer == null || binding.renderer.sharedMesh == null ||
                    (binding.renderer.transform != root.transform && !binding.renderer.transform.IsChildOf(root.transform)) ||
                    !renderers.Add(binding.renderer) || !SingleRegion(binding.region))
                { error = "Invalid or duplicate body region renderer: " + binding?.bodyId; return false; }
                var existing = bodyRegions.TryGetValue(body.id, out var value) ? value : ProfileBodyRegion.None;
                bodyRegions[body.id] = existing | binding.region;
                // Segments are visual only: a body mask must never contain a
                // skinned renderer using a bone under a switchable variant.
                foreach (var bone in binding.renderer.bones)
                    if (bone == null || (bone != transform && !bone.IsChildOf(transform)) ||
                        InsideVariant(bone, variants))
                    { error = "Body region has a nonpersistent bone: " + body.id; return false; }
                var rootBone = binding.renderer.rootBone;
                if (binding.renderer.bones.Length == 0 ||
                    binding.renderer.sharedMesh.bindposes.Length != binding.renderer.bones.Length ||
                    rootBone == null || (rootBone != transform && !rootBone.IsChildOf(transform)) ||
                    InsideVariant(rootBone, variants))
                { error = "Body region requires a shared skeleton and matching bind poses: " + body.id; return false; }
            }

            var covered = ProfileBodyRegion.None;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in items)
            {
                var item = CharacterCatalog.Item(rule?.itemId);
                if (item == null || item.slot == "body" || !variants.ContainsKey(item.id) ||
                    !ids.Add(item.id) || rule.hides == ProfileBodyRegion.None ||
                    (rule.hides & ~ProfileBodyRegion.All) != 0)
                { error = "Invalid or duplicate body coverage rule: " + rule?.itemId; return false; }
                covered |= rule.hides;
            }
            foreach (var pair in variants)
            {
                if (CharacterCatalog.Item(pair.Key).slot != "body") continue;
                if (!bodyRegions.TryGetValue(pair.Key, out var authored) || (authored & covered) != covered)
                { error = "Missing body regions for coverage rules: " + pair.Key; return false; }
            }
            return true;
        }

        static bool InsideVariant(Transform bone, Dictionary<string, GameObject> variants)
        {
            foreach (var visual in variants.Values)
                if (bone == visual.transform || bone.IsChildOf(visual.transform)) return true;
            return false;
        }

        static bool SingleRegion(ProfileBodyRegion region)
        {
            int value = (int)region;
            return value > 0 && (value & (value - 1)) == 0 && (region & ~ProfileBodyRegion.All) == 0;
        }

        // Called only after the entire outfit and all authored bindings validate.
        // Set every managed renderer on each application so removal restores
        // skin and changing body variants never inherits a stale mask.
        internal void ApplyValidated(HashSet<string> visibleIds)
        {
            var hidden = ProfileBodyRegion.None;
            foreach (var item in items)
                if (visibleIds.Contains(item.itemId)) hidden |= item.hides;
            foreach (var region in regions)
                region.renderer.enabled = (hidden & region.region) == 0;
        }
    }
}
