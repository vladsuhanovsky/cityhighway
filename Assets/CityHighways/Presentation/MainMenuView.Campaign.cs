using System;
using CityHighways.Core;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    public sealed partial class MainMenuView
    {
        VisualElement _campaignBody;

        VisualElement BuildCampaign()
        {
            var page = Ui.Column();
            page.Add(PageTitle(CampaignText.Title, () => ShowPage(Page.NewGame)));
            _campaignBody = Ui.Column();
            page.Add(_campaignBody);
            return page;
        }

        void RefreshCampaign()
        {
            if (_campaignBody == null) return;
            _campaignBody.Clear();
            var profile = Profiles.Active;
            string owner = CampaignStorage.Owner(profile, true);
            if (!CampaignStorage.TryRead(owner, out int completed))
            {
                _campaignBody.Add(Ui.Text(CampaignText.T("Campaign progress could not be read. Check profile storage.", "Не удалось прочитать прогресс кампании. Проверьте сохранения профиля."), 16f, Theme.Danger));
                return;
            }
            var intro = Ui.Text(CampaignText.T($"{profile.name}: {completed}/{CampaignCatalog.All.Count} missions complete. Fixed seeds; campaign-only progress.", $"{profile.name}: пройдено {completed}/{CampaignCatalog.All.Count}. Фиксированная раздача, отдельный прогресс."), 16f, Theme.Text);
            intro.style.whiteSpace = WhiteSpace.Normal;
            intro.style.marginBottom = 10f;
            _campaignBody.Add(intro);
            var checkpoint = SaveSystem.LoadCampaign(owner);
            if (checkpoint != null)
            {
                var mission = CampaignCatalog.Find(checkpoint.setup.campaignMission);
                var row = Ui.Row();
                row.style.marginBottom = 8f;
                var resume = Ui.Button(CampaignText.T("Resume: ", "Продолжить: ") + CampaignText.MissionTitle(mission),
                    () => _app.ContinueGame(checkpoint), ButtonKind.Primary, 15f);
                Ui.SetEnabled(resume, CampaignStorage.CanEnter(checkpoint.setup));
                row.Add(resume);
                var discard = Ui.Button(CampaignText.T("Discard checkpoint", "Удалить сохранение"), () =>
                {
                    SaveSystem.Delete(checkpoint.setup);
                    RefreshCampaign();
                }, ButtonKind.Secondary, 14f);
                discard.style.marginLeft = 8f;
                row.Add(discard);
                _campaignBody.Add(row);
            }

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.height = 420f;
            _campaignBody.Add(scroll);
            for (int i = 0; i < CampaignCatalog.All.Count; i++)
            {
                var mission = CampaignCatalog.All[i];
                bool unlocked = CampaignCatalog.Unlocked(mission.Id, completed);
                var card = Ui.Card(12f);
                card.style.marginBottom = 8f;
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                var content = Ui.Column();
                content.style.flexGrow = 1f;
                content.style.flexShrink = 1f;
                var title = Ui.Text($"{i + 1:00} · {CampaignText.MissionTitle(mission)}", 17f, i < completed ? Theme.Gold : Theme.Text, true);
                content.Add(title);
                string map = MagnateLocationText.Name(GameSetup.MapOf(mission.Map).Title);
                var detail = Ui.Text(map + " · " + ScenarioMenuLocalization.Title(mission.Scenario) + " · " +
                    (mission.Coop ? CampaignText.T("Co-op", "Кооператив") + " / " + CoopDifficultyText.Title((int)mission.Difficulty) : CampaignText.T("Competitive", "Соревнование")) +
                    " · " + CampaignText.Goal(mission), 13f, Theme.TextDim);
                detail.style.whiteSpace = WhiteSpace.Normal;
                content.Add(detail);
                row.Add(content);
                var button = Ui.Button(!unlocked ? CampaignText.T("Locked", "Закрыто") : i < completed ? CampaignText.T("Replay", "Повторить") : CampaignText.T("Start", "Начать"),
                    () => _app.StartGame(mission.CreateSetup(profile.name, owner, _setup.sound)), ButtonKind.Secondary, 14f);
                button.style.width = 135f;
                button.style.marginLeft = 12f;
                Ui.SetEnabled(button, unlocked && checkpoint == null);
                row.Add(button);
                card.Add(row);
                scroll.Add(card);
            }
        }
    }
}
