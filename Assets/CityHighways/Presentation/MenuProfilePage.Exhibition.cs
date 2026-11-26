using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    public sealed partial class MenuProfilePage
    {
        VisualElement ExhibitionCard(PlayerProfile profile)
        {
            var card = Card(14f);
            card.name = "profile-exhibition";
            card.style.marginTop = 12f;
            card.Add(Ui.Text(LocalizationService.Get("profile.exhibit.title"), 18f, Theme.Text, true));
            card.Add(Caption(LocalizationService.Get("profile.exhibit.hint"), 13f, Theme.TextDim, 0f, 4f));
            var available = ProfileExhibition.All.Where(e => e.IsUnlocked(profile)).ToList();
            card.Add(Ui.Text(string.Format(LocalizationService.Get("profile.exhibit.progress"), available.Count,
                ProfileExhibition.All.Count), 14f, GameColors.Accent, true));
            var saved = ProfileExhibition.Read(Profiles.Book, profile);
            var ids = new List<string> { "" };
            ids.AddRange(available.Select(e => e.Id));
            var labels = ids.Select(id => id.Length == 0 ? LocalizationService.Get("profile.exhibit.empty") :
                LocalizationService.Get("profile.exhibit.item." + id)).ToList();
            var row = Ui.Row();
            row.style.flexWrap = Wrap.Wrap;
            for (int slot = 0; slot < ProfileExhibition.SlotCount; slot++)
            {
                int index = slot;
                var pedestal = Card(10f);
                pedestal.style.width = 224f;
                pedestal.style.marginRight = 8f;
                pedestal.style.marginBottom = 8f;
                pedestal.Add(Ui.Text(string.Format(LocalizationService.Get("profile.exhibit.slot"), slot + 1), 14f, Theme.Text, true));
                var selector = new DropdownField("", new List<string>(labels), Mathf.Max(0, ids.IndexOf(saved[slot])));
                var preview = Ui.Column();
                preview.style.height = 100f;
                preview.style.justifyContent = Justify.Center;
                preview.style.alignItems = Align.Center;
                string Selected() => selector.index >= 0 && selector.index < ids.Count ? ids[selector.index] : "";
                void Draw()
                {
                    preview.Clear();
                    var exhibit = ProfileExhibition.Find(Selected());
                    if (exhibit?.Vehicle != null)
                    {
                        var vehicle = new FleetVehiclePreview();
                        vehicle.Set(FleetLivery.Read(Profiles.Book, profile, exhibit.Vehicle));
                        preview.Add(vehicle);
                    }
                    else
                    {
                        var label = Ui.Text(LocalizationService.Get(exhibit == null ? "profile.exhibit.empty" :
                            "profile.exhibit.item." + exhibit.Id), 15f, Theme.TextDim, true);
                        label.style.whiteSpace = WhiteSpace.Normal;
                        preview.Add(label);
                    }
                }
                selector.RegisterValueChangedCallback(_ => Draw());
                Draw();
                pedestal.Add(preview);
                pedestal.Add(selector);
                pedestal.Add(Btn(LocalizationService.Get("profile.exhibit.save"), () =>
                {
                    if (Profiles.Active != profile) return;
                    var book = Profiles.Book;
                    var previous = book.companies == null ? null : new List<CompanyIdentityRecord>(book.companies);
                    bool ok = ProfileExhibition.Store(book, profile, index, Selected()) && Profiles.TrySaveLocal();
                    if (!ok) book.companies = previous;
                    _status = LocalizationService.Get(ok ? "profile.exhibit.saved" : "profile.company.failed");
                    Rebuild();
                }, ButtonKind.Secondary, 13f));
                row.Add(pedestal);
            }
            card.Add(row);
            foreach (var group in new[] { "vehicles", "cities", "medals" })
            {
                card.Add(Ui.Text(LocalizationService.Get("profile.exhibit.group." + group), 14f, Theme.Text, true));
                foreach (var exhibit in ProfileExhibition.All.Where(e => e.Group == group))
                {
                    var item = Ui.Row();
                    var title = Ui.Text(LocalizationService.Get("profile.exhibit.item." + exhibit.Id), 12f, Theme.Text);
                    title.style.flexGrow = 1f;
                    item.Add(title);
                    item.Add(Ui.Text(LocalizationService.Get(exhibit.IsUnlocked(profile) ?
                        "profile.exhibit.unlocked" : "profile.exhibit.locked"), 12f, Theme.TextDim));
                    card.Add(item);
                    if (!exhibit.IsUnlocked(profile))
                    {
                        var achievement = Achievements.All.FirstOrDefault(a => a.Id == exhibit.Achievement);
                        if (achievement != null) card.Add(Caption(achievement.Description, 11f, Theme.TextDim, 0f, 2f));
                        else if (exhibit.Map != null) card.Add(Caption(LocalizationService.Get("profile.exhibit.play_map"), 11f, Theme.TextDim, 0f, 2f));
                    }
                }
            }
            return card;
        }
    }
}
