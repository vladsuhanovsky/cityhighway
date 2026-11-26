using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Shared Classic/Magnate control. Only present when an actual local city avatar
    /// is visible; no action dispatch, modal or independent camera.</summary>
    public sealed class CharacterObservationControl : VisualElement
    {
        readonly MapView map;
        readonly Button toggle;
        bool available;
        Vector3 position;
        string language;
        bool lastActive;

        public CharacterObservationControl(MapView map)
        {
            this.map = map;
            name = "character-observation";
            style.marginTop = 4f;
            toggle = Ui.Button("", Toggle, ButtonKind.Secondary, 13f);
            toggle.name = "character-observation-toggle";
            toggle.style.minHeight = 32f;
            Add(toggle);
            Ui.Show(this, false);
        }

        void Toggle()
        {
            if (map.IsObservingCharacter) map.EndCharacterObservation(true);
            else if (available) map.BeginCharacterObservation(position);
            UpdateLabel();
        }

        public void Sync(City3DScene city)
        {
            available = city != null && map.CanObserveCharacter &&
                city.TryGetProfilePedestrianPosition(out position);
            if (!available) map.EndCharacterObservation(true);
            else if (map.IsObservingCharacter) map.UpdateCharacterObservation(position);
            if ((style.display == DisplayStyle.None) == available) Ui.Show(this, available);
            UpdateLabel();
        }

        void UpdateLabel()
        {
            string current = LocalizationService.Language;
            bool active = map.IsObservingCharacter;
            if (language == current && lastActive == active) return;
            language = current;
            lastActive = active;
            toggle.text = LocalizationService.Get(active ? "character.observation.return" : "character.observation.find");
            toggle.tooltip = LocalizationService.Get(active ? "character.observation.return_hint" : "character.observation.find_hint");
        }
    }
}
