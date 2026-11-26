using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Shared Classic/Magnate control. Only present when an actual local city avatar
    /// is visible; no action dispatch, modal or independent camera.</summary>
    public sealed class CharacterObservationControl : VisualElement
    {
        readonly MapView map;
        readonly Button toggle, portraitButton, captureButton;
        readonly Label photoStatus;
        readonly VisualElement portrait, image;
        City3DScene currentCity;
        RenderTexture shownTexture;
        bool portraitOpen;
        float portraitYaw = 35f, portraitZoom = 1f;
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
            portraitButton = Ui.Button(LocalizationService.Get("character.observation.closeup"), TogglePortrait, ButtonKind.Secondary, 13f);
            portraitButton.name = "character-closeup-toggle";
            Add(portraitButton);
            portrait = new VisualElement { name = "character-closeup-panel" };
            portrait.style.width = 220f;
            portrait.style.backgroundColor = Theme.PanelGlass;
            Ui.Padding(portrait, 6f, 6f);
            Ui.Radius(portrait, 8f);
            image = new VisualElement();
            image.style.height = 138f;
            image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            portrait.Add(image);
            var row = Ui.Row();
            void Action(string label, System.Action action)
            {
                var button = Ui.Button(label, action, ButtonKind.Secondary, 13f);
                button.style.flexGrow = 1f;
                row.Add(button);
            }
            Action("←", () => portraitYaw -= 30f);
            Action("→", () => portraitYaw += 30f);
            Action("−", () => portraitZoom = Mathf.Max(.8f, portraitZoom - .1f));
            Action("+", () => portraitZoom = Mathf.Min(1.4f, portraitZoom + .1f));
            portrait.Add(row);
            captureButton = Ui.Button(LocalizationService.Get("character.photo.capture"), Capture, ButtonKind.Secondary, 13f);
            captureButton.name = "character-photo-capture";
            captureButton.SetEnabled(false);
            portrait.Add(captureButton);
            photoStatus = Ui.Text("", 11f, Theme.TextDim);
            photoStatus.style.whiteSpace = WhiteSpace.Normal;
            portrait.Add(photoStatus);
            Add(portrait);
            Ui.Show(portrait, false);
            RegisterCallback<DetachFromPanelEvent>(_ => ClosePortrait());
            Ui.Show(this, false);
        }

        void Toggle()
        {
            if (map.IsObservingCharacter) map.EndCharacterObservation(true);
            else if (available) map.BeginCharacterObservation(position);
            UpdateLabel();
        }

        void TogglePortrait()
        {
            if (portraitOpen) { ClosePortrait(); return; }
            if (!available || currentCity == null) return;
            portraitOpen = true;
            SyncPortrait();
        }

        void Capture()
        {
            if (!portraitOpen || currentCity == null || !currentCity.CanCaptureCharacterPhoto) return;
            bool saved = currentCity.TryCaptureCharacterPhoto(out var path);
            photoStatus.text = saved ? LocalizationService.Get("character.photo.saved") :
                LocalizationService.Get("character.photo.failed");
            photoStatus.tooltip = saved ? path : CharacterPhoto.Folder;
        }

        void SyncPortrait()
        {
            if (!portraitOpen) return;
            if (currentCity == null || !available ||
                !currentCity.TryShowCharacterCloseup(portraitYaw, portraitZoom, out var texture))
            { ClosePortrait(); return; }
            if (shownTexture != texture)
            {
                shownTexture = texture;
                image.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
            }
            captureButton.text = LocalizationService.Get("character.photo.capture");
            captureButton.SetEnabled(currentCity.CanCaptureCharacterPhoto);
            Ui.Show(portrait, true);
            portraitButton.text = LocalizationService.Get("character.observation.close_closeup");
        }

        void ClosePortrait()
        {
            portraitOpen = false;
            captureButton.SetEnabled(false);
            photoStatus.text = "";
            photoStatus.tooltip = "";
            image.style.backgroundImage = StyleKeyword.None;
            shownTexture = null;
            Ui.Show(portrait, false);
            currentCity?.CloseCharacterCloseup();
            portraitButton.text = LocalizationService.Get("character.observation.closeup");
        }

        public void Sync(City3DScene city)
        {
            if (!ReferenceEquals(currentCity, city))
            {
                ClosePortrait();
                currentCity = city;
            }
            available = city != null && map.CanObserveCharacter &&
                city.TryGetProfilePedestrianPosition(out position);
            if (!available) map.EndCharacterObservation(true);
            else if (map.IsObservingCharacter) map.UpdateCharacterObservation(position);
            if ((style.display == DisplayStyle.None) == available) Ui.Show(this, available);
            if (!available && portraitOpen) ClosePortrait();
            else if (portraitOpen) SyncPortrait();
            else portraitButton.text = LocalizationService.Get("character.observation.closeup");
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
