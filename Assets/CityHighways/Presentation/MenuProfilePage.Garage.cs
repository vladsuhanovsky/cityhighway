using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    public sealed partial class MenuProfilePage
    {
        VisualElement GarageCard(PlayerProfile profile)
        {
            var garage = Card(14f);
            garage.name = "profile-garage";
            garage.style.marginTop = 12f;
            garage.Add(Ui.Text(LocalizationService.Get("profile.garage.title"), 18f, Theme.Text, true));
            garage.Add(Caption(LocalizationService.Get("profile.garage.hint"), 13f, Theme.TextDim, 0f, 4f));
            var company = CompanyIdentity.Read(Profiles.Book, profile);
            if (!string.IsNullOrEmpty(company.Name))
                garage.Add(Caption(company.Name, 14f, GameColors.Accent, 0f, 4f));
            garage.Add(Btn(LocalizationService.Get("profile.garage.edit_company"), () => Navigate(ProfileSection.Overview), ButtonKind.Secondary, 13f));
            var row = Ui.Row();
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 8f;
            foreach (string vehicle in FleetLivery.VehicleIds)
                row.Add(GarageVehicleCard(profile, vehicle));
            garage.Add(row);
            return garage;
        }

        VisualElement GarageVehicleCard(PlayerProfile profile, string vehicle)
        {
            var current = FleetLivery.Read(Profiles.Book, profile, vehicle);
            var card = Card(10f);
            card.name = "garage-" + vehicle;
            card.style.width = 224f;
            card.style.marginRight = 8f;
            card.style.marginBottom = 8f;
            card.style.alignItems = Align.Stretch;
            card.Add(Ui.Text(LocalizationService.Get("profile.garage.vehicle." + vehicle), 16f, Theme.Text, true));
            var preview = new FleetVehiclePreview();
            preview.style.alignSelf = Align.Center;
            card.Add(preview);
            var schemes = FleetLivery.SchemeIds.ToList();
            var paint = new DropdownField("", schemes.Select(id => LocalizationService.Get("profile.garage.scheme." + id)).ToList(),
                schemes.IndexOf(current.Scheme));
            paint.name = "garage-scheme-" + vehicle;
            card.Add(paint);
            var emblem = new Toggle(LocalizationService.Get("profile.garage.show_emblem")) { value = !current.HideEmblem };
            card.Add(emblem);
            string Scheme() => paint.index >= 0 && paint.index < schemes.Count ? schemes[paint.index] : "company";
            void Refresh() => preview.Set(FleetLivery.Preview(Profiles.Book, profile, vehicle, Scheme(), !emblem.value));
            paint.RegisterValueChangedCallback(_ => Refresh());
            emblem.RegisterValueChangedCallback(_ => Refresh());
            Refresh();
            var save = Btn(LocalizationService.Get("profile.garage.save"), () =>
            {
                if (Profiles.Active != profile) return;
                var book = Profiles.Book;
                var previous = book.companies == null ? null : new List<CompanyIdentityRecord>(book.companies);
                bool saved = FleetLivery.Store(book, profile, vehicle, Scheme(), !emblem.value) && Profiles.TrySaveLocal();
                if (!saved) book.companies = previous;
                _status = LocalizationService.Get(saved ? "profile.garage.saved" : "profile.company.failed");
                Rebuild();
            }, ButtonKind.Secondary, 13f);
            card.Add(save);
            return card;
        }
    }
}
