using CityHighways.Core;

namespace CityHighways.Presentation
{
    public static class CampaignText
    {
        public static bool English => LocalizationService.Language == "en";
        public static string T(string en, string ru) => English ? en : ru;
        public static string Title => T("Campaign", "Кампания");
        public static string MissionTitle(CampaignMission mission) => English ? mission.En : mission.Ru;
        public static string Goal(CampaignMission mission)
        {
            string win = mission.Coop ? T("Team victory", "Победа команды") : T("Win the match", "Победить в партии");
            switch (mission.Goal)
            {
                case CampaignGoal.Contracts: return win + T($"; complete at least {mission.Target} contracts", $"; выполнить не менее {mission.Target} контрактов");
                case CampaignGoal.Bridges: return win + T($"; build at least {mission.Target} bridges", $"; построить не менее {mission.Target} мостов");
                case CampaignGoal.RoadPieces: return win + T($"; build at least {mission.Target} road pieces", $"; построить не менее {mission.Target} участков");
                case CampaignGoal.TeamCrisis: return win + T($"; finish with crisis at most {mission.Target}", $"; завершить с кризисом не выше {mission.Target}");
                default: return win;
            }
        }

        public static string Result(bool passed, bool saved) => !passed ? T("Objective not achieved", "Цель не выполнена")
            : saved ? T("Mission completed", "Миссия пройдена") : T("Mission passed; progress not saved", "Миссия пройдена; прогресс не сохранён");
    }
}
