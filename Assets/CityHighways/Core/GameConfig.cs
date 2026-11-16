using System;

namespace CityHighways.Core
{
    /// <summary>Все числа правил в одном месте — удобно балансировать.</summary>
    [Serializable]
    public sealed class GameConfig
    {
        public int MinPlayers = 2;
        public int MaxPlayers = 5;

        // ------------------------------------------------------------ Сценарий (Scenarios.Apply выставляет поля ниже)
        /// <summary>Сценарий партии (ScenarioKind как int): 0 — обычная Москва.</summary>
        public int Scenario;
        /// <summary>Зима: щебёночные дороги всю партию на SnowExtraCost карт дороже (разрешение мэрии снимает на ход).</summary>
        public bool PermanentSnow;
        /// <summary>Зима: в снегопад дорожает и бетон.</summary>
        public bool SnowHitsConcrete;
        /// <summary>Большая Москва: окраины открыты с начала партии.</summary>
        public bool ExpansionsOpen;
        /// <summary>Сколько участков добавить каждому сверх обычного (не действует при RoadPiecesOverride).</summary>
        public int ExtraRoadPieces;
        /// <summary>Год ремонта: столько свободных участков закрыто каждый круг; следующие видны заранее.</summary>
        public int ScheduledRepairs;
        /// <summary>Год ремонта: влияние за каждую свою дорогу длиной LongRoadLength и больше.</summary>
        public int InfluencePerLongRoad;
        public int LongRoadLength = 3;
        /// <summary>Выборы мэра: сколько аукционов за партию; второй — когда кто-то построил SecondAuctionShare участков.</summary>
        public int AuctionCount = 1;
        public float SecondAuctionShare = 0.67f;
        /// <summary>Выборы мэра: «друг мэра» — больше всех потративший влияния за партию получает столько очков.</summary>
        public int MayorFriendBonus;
        /// <summary>Выбор компании: в начале партии каждому, кто выбирает, предлагается столько случайных персонажей.</summary>
        public int CharacterOffer = 2;

        /// <summary>Сколько участков дороги у каждого игрока, по числу игроков (индекс — число игроков).</summary>
        public int[] RoadPiecesByPlayers = { 0, 0, 35, 32, 28, 25 };
        /// <summary>Если больше нуля — у всех ровно столько участков, независимо от числа игроков.</summary>
        public int RoadPiecesOverride;

        public int StartingHandSize = 4;

        /// <summary>Карт каждого ресурса: асфальт, бетон, сталь, щебень. Стали мало — мосты дефицитны.</summary>
        public int[] CardsPerResource = { 26, 24, 10, 18 };
        public int MachineryCards = 14;

        public int FaceUpSlots = 5;
        /// <summary>Если открытой техники больше этого числа, открытый ряд выкладывается заново.</summary>
        public int MaxFaceUpMachinery = 2;

        public int InitialContractsDealt = 3;
        public int InitialContractsMinKeep = 2;
        public int ContractsDrawn = 3;
        public int ContractsMinKeep = 1;

        /// <summary>Когда у игрока после хода остаётся столько участков или меньше, начинается последний круг.</summary>
        public int FinalRoundPieces = 2;
        /// <summary>«Скоро финал»: объявляется, когда у кого-то осталось столько участков или меньше (0 — не объявлять).</summary>
        public int FinalWarningPieces = 6;
        /// <summary>Финальный заказ мэрии: очки тому, кто построит выбранную дорогу в последнем круге (0 — без заказа).</summary>
        public int FinalOrderBonus = 5;
        /// <summary>Проверка перед итогами: шанс (в процентах) раскрыть каждую ещё нераскрытую взятку (0 — без проверки).</summary>
        public int FinalAuditPercent = 50;

        /// <summary>Сколько очков платит игрок владельцу за каждый чужой участок на пути контракта.</summary>
        public int TollPerRoad = 2;
        /// <summary>Сколько чужих участков можно использовать в одном контракте.</summary>
        public int MaxForeignRoadsPerContract = 2;

        /// <summary>Городские события: раз в круг открывается событие и действует до следующего круга.</summary>
        public bool EventsEnabled = true;
        /// <summary>Во сколько раз дороже проезд по дорогам, построенным в «Час пик».</summary>
        public int RushHourTollMultiplier = 2;
        /// <summary>Сколько свободных участков закрывает «Ремонт».</summary>
        public int RepairRoads = 2;
        /// <summary>На сколько карт дешевле мост во время «Субсидии мэрии» (минимум одна карта).</summary>
        public int BridgeSubsidy = 1;
        /// <summary>Сколько карт каждого события в колоде; «Новый район» — по одной на каждый район-расширение.</summary>
        public int RushHourEvents = 3;
        public int RepairEvents = 3;
        public int SubsidyEvents = 3;

        /// <summary>
        /// Персонажи. Подобрано на модели (тысячи партий умных ботов): за столом на 4 игрока каждый
        /// выигрывает 23–28% партий, в дуэлях 45–53%.
        /// </summary>
        public int ForemanFreeRoads = 1;
        public int ForemanMaxLength = 2;
        public int InsurerFine = 1;
        public int TollmanToll = 4;
        public int OfficialBonusPerContract = 2;
        public int BridgerDiscount = 1;
        public int BridgerPointsPerBridge = 1;

        /// <summary>Новые события: Парад, Снегопад, Инвестор — по 2 в колоде.</summary>
        /// <summary>Шанс, что в начале круга откроется событие (0..1). В остальных кругах — спокойный день.</summary>
        public float EventChance = 0.5f;

        public int ParadeEvents = 2;
        public int SnowEvents = 2;
        public int InvestorEvents = 2;
        public string ParadeDistrict = "center";
        public int SnowExtraCost = 1;
        public int InvestorBonus = 2;

        /// <summary>Улучшение дороги: своя дорога становится магистралью (проезд ×RushHourTollMultiplier)
        /// за половину её длины в картах (с округлением вверх), это действие хода, +UpgradePoints очков.</summary>
        public bool UpgradeEnabled = true;
        public int UpgradePoints = 2;

        /// <summary>Сделки: раз в ход до основного действия предложить обмен 1–TradeMaxCards карт одному сопернику.</summary>
        public bool TradesEnabled = true;
        public int TradeMaxCards = 3;

        /// <summary>
        /// Влияние — особая валюта. Начисляется за выполненные контракты, расширение сети в новые районы,
        /// цели мэрии, «Грант» удачи и отставание; тратится раз в ход, не занимая ход. Очков в конце не даёт:
        /// остаток уходит в кошелёк профиля. Числа подобраны на модели Tools~/ch_influence.py.
        /// </summary>
        public bool InfluenceEnabled = true;
        public int InfluencePerContract = 1;
        public int InfluencePerComplexContract = 2;
        /// <summary>Дорога ведёт в район, где у компании ещё не было дорог (кроме самой первой дороги).</summary>
        public int InfluencePerNewDistrict = 1;
        public int InfluencePerGoal = 1;
        /// <summary>В начале круга отстающим от лидера на InfluenceCatchUpGap очков и больше (по видимым очкам).</summary>
        public int InfluenceCatchUp = 1;
        public int InfluenceCatchUpGap = 6;
        /// <summary>«Грант мэрии» при включённом влиянии даёт влияние вместо очков.</summary>
        public int LuckGrantInfluence = 3;
        /// <summary>
        /// Обновить рынок: первое — InfluenceRefreshCost, каждое следующее у того же игрока на InfluenceRefreshStep дороже.
        /// Модель (Tools~/ch_influence.py): при 3 за любое обновление бот, который обновляет рынок, выигрывал ~59% дуэлей
        /// и ~41% партий на троих; при 4 и +2 — ~54% и ~37%, на четверых ~28% (база 50/33/25%).
        /// </summary>
        public int InfluenceRefreshCost = 4;
        public int InfluenceRefreshStep = 2;
        public int InfluenceExtraContractCost = 1;
        public int InfluencePermitCost = 1;
        public int InfluenceCancelEventCost = 3;
        /// <summary>Сколько влияния можно положить в сделку с каждой стороны.</summary>
        public int TradeMaxInfluence = 5;

        /// <summary>
        /// Аукцион мэрии: раз за партию, в начале круга после того, как кто-то построил AuctionTriggerShare своих
        /// участков. Разыгрывается самый дорогой из AuctionCandidates верхних контрактов колоды; выполнен — +AuctionBonus.
        /// </summary>
        public bool AuctionEnabled = true;
        public float AuctionTriggerShare = 0.34f;
        public int AuctionCandidates = 5;
        public int AuctionBonus = 5;

        /// <summary>
        /// Взятки чиновникам: тайно, раз в ход, за влияние. Раскрыта соперником, пока давший не начал следующий ход, —
        /// штраф BribeFine очков за каждую взятку, разоблачителю +BribeWhistleReward влияния. Ложное обвинение — −FalseAccusationFine.
        /// </summary>
        public bool BribesEnabled = true;
        public int BribeDiscountCost = 2;
        public int BribeExtraCardCost = 2;
        /// <summary>
        /// Штраф за раскрытую взятку по числу игроков (индекс — число игроков). Подобрано на модели Tools~/ch_politics.py
        /// так, чтобы ни «всегда давать», ни «никогда не давать», ни «обвинять при каждой улике» не выигрывали заметно чаще.
        /// Больше нуля в BribeFine — один штраф для всех.
        /// </summary>
        public int[] BribeFineByPlayers = { 0, 0, 0, 6, 4, 4 };
        public int BribeFine;
        /// <summary>Взятки — только когда игроков не меньше: вдвоём улики выдают взятку почти всегда, тайны не выходит.</summary>
        public int BribesMinPlayers = 3;
        public int BribeWhistleReward = 2;
        public int FalseAccusationFine = 3;

        /// <summary>
        /// Пробки: контракт платит проезд в момент выполнения, его маршрут фиксируется. Обычная дорога, по которой едут
        /// JamThreshold и больше маршрутов соперников владельца, — в пробке: следующий проезд стоит ещё JamFee очков
        /// (они сгорают). Магистраль в пробку не встаёт. 0 — без пробок. Подобрано на модели Tools~/ch_traffic.py:
        /// за столом на 4 к концу партии в пробке около трёх дорог, в дуэли — меньше одной.
        /// </summary>
        public int JamThreshold = 2;
        public int JamFee = 2;
        /// <summary>Пропуск: в сделке можно отдать бесплатный проезд по всем своим дорогам до конца партии.</summary>
        public bool PassesEnabled = true;

        /// <summary>Сколько общих целей мэрии открывается в начале партии (из пула карты).</summary>
        public int MayorGoalsDealt = 3;

        /// <summary>Колода удачи: карту можно взять вместо одной из двух карт ресурсов, не больше одной за ход.</summary>
        public bool LuckEnabled = true;
        public int LuckGrant = 5;
        public int LuckSupply = 3;
        public int LuckPremiumPerContract = 3;
        public int LuckPremiumMax = 9;
        public int LuckFine = 3;
        public int LuckTheft = 1;
        /// <summary>Состав колоды удачи: 11 бафов, 7 дебафов. Подобрано на модели: в среднем удача не хуже карты вслепую.</summary>
        public int LuckGrantCards = 4;
        public int LuckSupplyCards = 4;
        public int LuckPremiumCards = 3;
        public int LuckFineCards = 3;
        public int LuckTheftCards = 2;
        public int LuckRecallCards = 2;

        /// <summary>Бонус «Крупнейшая сеть»: тем, чья одна связная сеть охватывает больше всего районов.</summary>
        public int NetworkBonus = 10;

        /// <summary>Очки за дорогу по длине (индекс — длина).</summary>
        public int[] RoadPointsByLength = { 0, 1, 2, 4, 7, 10, 15 };

        // ------------------------------------------------------------ Кооператив «Город против всех»

        /// <summary>
        /// Кооператив: все компании — одна команда против города. Сеть общая (дороги союзников засчитываются в контракты
        /// и бесплатны), на столе — заказы города с таймером, растёт кризис. Победа — выполнить нужное число заказов,
        /// поражение — кризис дошёл до предела или участки кончились раньше. Взяток, аукциона и пропусков нет.
        /// </summary>
        public bool Cooperative;
        /// <summary>Сколько заказов города открыто одновременно.</summary>
        public int CoopOpenOrders = 3;
        /// <summary>За сколько кругов нужно выполнить заказ.</summary>
        public int CoopOrderRounds = 5;
        /// <summary>Шкала кризиса: на этом значении город проигрывает.</summary>
        public int CoopCrisisMax = 10;
        public int CoopCrisisStart = 2;
        /// <summary>Город растёт: кризис +1 каждые столько кругов.</summary>
        public int CoopCrisisEveryRounds = 2;
        /// <summary>Просроченный заказ поднимает кризис на столько.</summary>
        public int CoopCrisisPerFailedOrder = 2;
        /// <summary>Выполненный заказ снижает кризис на столько.</summary>
        public int CoopCrisisReliefPerOrder = 2;
        /// <summary>Ремонт, снегопад или парад поднимают кризис на столько.</summary>
        public int CoopCrisisPerBadEvent = 1;
        /// <summary>Сколько заказов нужно выполнить для победы (по числу игроков).</summary>
        public int[] CoopOrdersToWinByPlayers = { 0, 0, 5, 6, 7, 8 };

        public int CoopOrdersToWin(int players) =>
            CoopOrdersToWinByPlayers[Math.Max(0, Math.Min(players, CoopOrdersToWinByPlayers.Length - 1))];

        /// <summary>Включить кооператив: соревновательные механики (взятки, аукцион, пропуска) выключаются.</summary>
        public GameConfig MakeCooperative()
        {
            Cooperative = true;
            BribesEnabled = false;
            AuctionEnabled = false;
            PassesEnabled = false;
            return this;
        }

        public int MaxRoadLength => RoadPointsByLength.Length - 1;

        public int TotalCards
        {
            get
            {
                int sum = MachineryCards;
                foreach (int count in CardsPerResource) sum += count;
                return sum;
            }
        }

        public int RoadPiecesFor(int players)
        {
            if (RoadPiecesOverride > 0) return RoadPiecesOverride;
            int index = Math.Max(0, Math.Min(players, RoadPiecesByPlayers.Length - 1));
            return RoadPiecesByPlayers[index] + ExtraRoadPieces;
        }

        /// <summary>Штраф за одну раскрытую взятку при таком числе игроков.</summary>
        public int BribeFineFor(int players)
        {
            if (BribeFine > 0) return BribeFine;
            if (BribeFineByPlayers == null || BribeFineByPlayers.Length == 0) return 4;
            int index = Math.Max(0, Math.Min(players, BribeFineByPlayers.Length - 1));
            return BribeFineByPlayers[index] > 0 ? BribeFineByPlayers[index] : 4;
        }

        public int PointsForLength(int length) =>
            length >= 0 && length < RoadPointsByLength.Length ? RoadPointsByLength[length] : 0;

        /// <summary>Копия настроек (массивы тоже копируются) — сценарий меняет копию, а не общий объект.</summary>
        public GameConfig Clone()
        {
            var copy = (GameConfig)MemberwiseClone();
            foreach (var field in typeof(GameConfig).GetFields())
            {
                if (field.GetValue(this) is Array array) field.SetValue(copy, array.Clone());
            }
            return copy;
        }
    }
}
