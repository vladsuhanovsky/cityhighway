using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Explicit 3D authoring sockets, not the old 2D canvas coordinates.</summary>
    public enum ProfileCharacterSocket { Body, Head, Chest, Hips, Feet, Hand }

    public sealed class ProfileCharacterRig : MonoBehaviour
    {
        public string gender = CharacterCatalog.Male;
        public Transform body, head, chest, hips, feet, hand;

        public Transform Socket(ProfileCharacterSocket socket)
        {
            switch (socket)
            {
                case ProfileCharacterSocket.Body: return body;
                case ProfileCharacterSocket.Head: return head;
                case ProfileCharacterSocket.Chest: return chest;
                case ProfileCharacterSocket.Hips: return hips;
                case ProfileCharacterSocket.Feet: return feet;
                case ProfileCharacterSocket.Hand: return hand;
                default: return null;
            }
        }

        public bool IsValid()
        {
            if (gender != CharacterCatalog.Male && gender != CharacterCatalog.Female) return false;
            var unique = new HashSet<Transform>();
            foreach (ProfileCharacterSocket socket in Enum.GetValues(typeof(ProfileCharacterSocket)))
            {
                var anchor = Socket(socket);
                if (anchor == null || anchor == transform || !anchor.IsChildOf(transform) || !unique.Add(anchor)) return false;
            }
            return true;
        }
    }
}
