namespace CityHighways.Core
{
    public enum GamePhase
    {
        /// <summary>Начало игры: каждый выбирает стартовые контракты.</summary>
        SetupContracts,
        /// <summary>Ход: взять карты, построить дорогу или взять контракты.</summary>
        AwaitingAction,
        /// <summary>Игрок взял первую карту и должен взять вторую.</summary>
        DrawingSecondCard,
        /// <summary>Игрок вытянул контракты и выбирает, какие оставить.</summary>
        ChoosingContracts,
        GameOver,
        /// <summary>Везунчик открыл 2 карты удачи и выбирает одну (в конце списка — чтобы не сдвинуть номера в старых сохранениях).</summary>
        ChoosingLuck,
        /// <summary>Получатель сделки решает: принять или отказаться (действует он, а не текущий игрок).</summary>
        RespondingTrade,
        /// <summary>Аукцион мэрии: игроки по кругу делают тайные ставки влиянием (действует тот, чья очередь ставить).</summary>
        Bidding
    }

    /// <summary>Результат действия: успех или понятная причина отказа для интерфейса.</summary>
    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string Error { get; }

        ActionResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }

        public static ActionResult Ok() => new ActionResult(true, null);
        public static ActionResult Fail(string error) => new ActionResult(false, error);

        public override string ToString() => Success ? "OK" : "Ошибка: " + Error;
    }

    /// <summary>Способ оплатить дорогу: ресурс и сколько добавить техники.</summary>
    public readonly struct PaymentOption
    {
        /// <summary>Ресурс оплаты. Machinery — дорога оплачивается только техникой.</summary>
        public Resource Resource { get; }
        public int Machinery { get; }

        public PaymentOption(Resource resource, int machinery)
        {
            Resource = resource;
            Machinery = machinery;
        }

        public override string ToString() => ResourceInfo.RussianName(Resource) + " + техника × " + Machinery;
    }
}
