namespace CityHighways.Core
{
    public enum CoopDifficultyKind { Normal = 0, Easy = 1, Hard = 2 }

    /// <summary>Resolve difficulty once when creating a new game. Saved concrete rules are authoritative.</summary>
    public static class CooperativeDifficulty
    {
        public static CoopDifficultyKind Normalize(int value) =>
            value == 1 ? CoopDifficultyKind.Easy : value == 2 ? CoopDifficultyKind.Hard : CoopDifficultyKind.Normal;

        public static CoopDifficultyKind Next(int value)
        {
            switch (Normalize(value))
            {
                case CoopDifficultyKind.Easy: return CoopDifficultyKind.Normal;
                case CoopDifficultyKind.Normal: return CoopDifficultyKind.Hard;
                default: return CoopDifficultyKind.Easy;
            }
        }

        public static GameConfig Apply(GameConfig baseConfig, CoopDifficultyKind kind)
        {
            var config = (baseConfig ?? new GameConfig()).Clone().MakeCooperative();
            kind = Normalize((int)kind);
            config.CoopDifficulty = (int)kind;
            switch (kind)
            {
                case CoopDifficultyKind.Easy:
                    config.CoopOrderRoundsBase = 6;
                    config.CoopStartStagger = 3;
                    config.CoopCrisisMax = 15;
                    config.CoopCrisisStart = 0;
                    config.CoopCrisisEveryRounds = 4;
                    config.CoopCrisisPerFailedOrder = 1;
                    config.CoopOrdersToWinByPlayers = new[] { 0, 0, 3, 4, 5, 6 };
                    break;
                case CoopDifficultyKind.Hard:
                    config.CoopOrderRoundsBase = 3;
                    config.CoopStartStagger = 1;
                    config.CoopCrisisMax = 10;
                    config.CoopCrisisStart = 2;
                    config.CoopCrisisEveryRounds = 2;
                    config.CoopCrisisPerFailedOrder = 3;
                    config.CoopOrdersToWinByPlayers = new[] { 0, 0, 5, 6, 7, 8 };
                    break;
            }
            return config;
        }
    }
}
