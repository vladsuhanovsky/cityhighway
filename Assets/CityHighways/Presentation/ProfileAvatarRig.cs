using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Authoring contract for a modular player avatar prefab. Variants can be
    /// skinned meshes or socket-mounted objects on the same skeleton. No runtime asset
    /// path resolution or mutation of ownership. The caller owns instance lifetime.</summary>
    public sealed class ProfileAvatarRig : MonoBehaviour
    {
        [Serializable]
        public sealed class Binding
        {
            public string itemId;
            public GameObject visual;
        }

        [SerializeField] string gender = CharacterCatalog.Male;
        [SerializeField] Binding[] bindings = Array.Empty<Binding>();

        public string Gender => gender;

#if UNITY_EDITOR
        // Used by the explicit prototype authoring tool only.
        internal void ConfigurePrototype(string value, Binding[] variants)
        {
            gender = value;
            bindings = variants;
        }
#endif

        public bool TryValidate(out string error)
        {
            return TryIndex(out _, out error);
        }

        bool TryIndex(out Dictionary<string, GameObject> index, out string error)
        {
            index = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            error = null;
            if (gender != CharacterCatalog.Male && gender != CharacterCatalog.Female)
            { error = "Unsupported rig gender."; return false; }
            if (bindings == null || bindings.Length == 0)
            { error = "No wardrobe bindings."; return false; }
            var objects = new HashSet<GameObject>();
            foreach (var binding in bindings)
            {
                var item = CharacterCatalog.Item(binding?.itemId);
                if (item == null || !item.Fits(gender))
                { error = "Unknown or incompatible item: " + binding?.itemId; return false; }
                var visual = binding.visual;
                if (visual == null || visual == gameObject || !visual.transform.IsChildOf(transform))
                { error = "Visual must be a child of this rig: " + item.id; return false; }
                if (index.ContainsKey(item.id) || !objects.Add(visual))
                { error = "Duplicate item or visual: " + item.id; return false; }
                index.Add(item.id, visual);
            }
            // Variant roots may not contain other variants: toggling a jacket must not
            // accidentally deactivate the skeleton, body, or another selected item.
            foreach (var visual in objects)
                for (var parent = visual.transform.parent; parent != null && parent != transform; parent = parent.parent)
                    if (objects.Contains(parent.gameObject))
                    { error = "Nested wardrobe variants: " + visual.name; return false; }
            return true;
        }

        /// <summary>Validate first, then apply all variants. An unsupported outfit leaves
        /// the previous instance unchanged; a caller can retain its existing 2D preview.
        /// Hidden items remain in the appearance snapshot, but need no active visual.</summary>
        public bool TryApply(ProfileAppearance appearance, out string error)
        {
            if (!TryIndex(out var index, out error)) return false;
            if (appearance == null || appearance.Gender != gender)
            { error = "Appearance does not match rig gender."; return false; }
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slot in CharacterCatalog.Slots)
            {
                string id = appearance.VisibleId(slot);
                if (string.IsNullOrEmpty(id)) continue;
                if (!index.ContainsKey(id))
                { error = "Missing visual binding: " + id; return false; }
                selected.Add(id);
            }
            foreach (var pair in index)
                if (pair.Value.activeSelf != selected.Contains(pair.Key))
                    pair.Value.SetActive(selected.Contains(pair.Key));
            return true;
        }

        // Shared animated bones must never be deactivated by changing wardrobe slots.
        public bool IsStableSkeletonTransform(Transform bone)
        {
            if (bone == null || (bone != transform && !bone.IsChildOf(transform)) ||
                !TryIndex(out var index, out _)) return false;
            foreach (var visual in index.Values)
                if (bone == visual.transform || bone.IsChildOf(visual.transform)) return false;
            return true;
        }

        public string[] MissingCatalogIds()
        {
            if (!TryIndex(out var index, out _)) return Array.Empty<string>();
            var missing = new List<string>();
            foreach (var slot in CharacterCatalog.Slots)
                foreach (var item in CharacterCatalog.ItemsIn(slot, gender))
                    if (!index.ContainsKey(item.id)) missing.Add(item.id);
            return missing.ToArray();
        }
    }
}
