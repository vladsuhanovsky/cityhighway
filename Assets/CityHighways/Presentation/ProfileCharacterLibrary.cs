using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityHighways.Presentation
{
    [CreateAssetMenu(menuName = "City Highways/Profile/Character 3D Library", fileName = "ProfileCharacterLibrary")]
    public sealed class ProfileCharacterLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class ItemBinding
        {
            public string itemId;
            public ProfileCharacterSocket socket;
            public GameObject prefab;
        }

        public ProfileCharacterRig male, female;
        public ItemBinding[] items = Array.Empty<ItemBinding>();

        public ProfileCharacterRig Rig(string gender) => gender == CharacterCatalog.Female ? female : male;

        public static ProfileCharacterSocket SocketFor(string slot)
        {
            switch (slot)
            {
                case "hair": case "hat": return ProfileCharacterSocket.Head;
                case "top": return ProfileCharacterSocket.Chest;
                case "bottom": return ProfileCharacterSocket.Hips;
                case "shoes": return ProfileCharacterSocket.Feet;
                case "accessory": return ProfileCharacterSocket.Hand;
                default: return ProfileCharacterSocket.Body;
            }
        }

        // Accessories can be worn on head/chest or held in the hand, as authored.
        public static bool SocketMatches(CharacterItem item, ProfileCharacterSocket socket) =>
            item != null && (item.slot == "accessory"
                ? socket == ProfileCharacterSocket.Head || socket == ProfileCharacterSocket.Chest || socket == ProfileCharacterSocket.Hand
                : SocketFor(item.slot) == socket);

        public string[] Audit()
        {
            var issues = new List<string>();
            foreach (string gender in new[] { CharacterCatalog.Male, CharacterCatalog.Female })
            {
                var rig = Rig(gender);
                if (rig == null || rig.gender != gender || !rig.IsValid()) issues.Add("invalid-rig:" + gender);
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in items ?? Array.Empty<ItemBinding>())
            {
                if (entry == null || string.IsNullOrEmpty(entry.itemId)) { issues.Add("empty-binding"); continue; }
                if (!seen.Add(entry.itemId)) issues.Add("duplicate:" + entry.itemId);
                var item = CharacterCatalog.Item(entry.itemId);
                if (item == null) issues.Add("unknown:" + entry.itemId);
                else if (!SocketMatches(item, entry.socket)) issues.Add("socket:" + entry.itemId);
                if (entry.prefab == null) issues.Add("missing-prefab:" + entry.itemId);
            }
            foreach (var item in CharacterCatalog.Slots.SelectMany(slot => CharacterCatalog.ItemsIn(slot)))
                if (!seen.Contains(item.id)) issues.Add("unbound:" + item.id);
            return issues.ToArray();
        }

        public bool TryResolve(ProfileCharacterAppearance look, out ProfileCharacterRig rig, out ItemBinding[] bindings)
        {
            rig = look == null ? null : Rig(look.Gender);
            bindings = Array.Empty<ItemBinding>();
            if (rig == null || rig.gender != look.Gender || !rig.IsValid()) return false;
            var result = new List<ItemBinding>();
            foreach (var slot in CharacterCatalog.Slots)
            {
                string id = look.Visible(slot);
                if (string.IsNullOrEmpty(id)) continue;
                var matches = (items ?? Array.Empty<ItemBinding>()).Where(i => i != null && i.itemId == id).ToArray();
                if (matches.Length != 1 || matches[0].prefab == null ||
                    !SocketMatches(CharacterCatalog.Item(id), matches[0].socket)) return false;
                result.Add(matches[0]);
            }
            bindings = result.ToArray();
            return true;
        }

        // Only authored prefab references are resolved. No arbitrary resource path from player data.
        // The caller owns the returned instance; missing/ambiguous binding returns null for legacy fallback.
        // Item prefabs must be authored as static/socket-local visuals; skinned clothing retargeting is future work.
        public GameObject InstantiateLook(ProfileCharacterAppearance look, Transform parent = null)
        {
            if (!TryResolve(look, out var rig, out var bindings)) return null;
            var instance = Instantiate(rig, parent, false);
            try
            {
                foreach (var binding in bindings)
                    Instantiate(binding.prefab, instance.Socket(binding.socket), false);
                return instance.gameObject;
            }
            catch
            {
                instance.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(instance.gameObject);
                else DestroyImmediate(instance.gameObject);
                throw;
            }
        }
    }
}
