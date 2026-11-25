using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>Read-only Classic gameplay tooltip and contract text. The screen supplies viewer and privacy policy; Core remains the authority for costs and legality.</summary>
    public static class GameTooltipText
    {
        static bool EnglishUi => LocalizationService.Language == "en";
        static string DisplayPlace(string name) => MagnateLocationText.Name(name);
        static string DistrictName(CityMap map, string id) => DisplayPlace(map.DistrictName(id));

        static string Plural(int n, string one, string few, string many)
        {
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return one;
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return few;
            return many;
        }

        public static string RoadKindLabel(Resource? resource)
        {
            if (!EnglishUi) return ResourceInfo.RoadKind(resource);
            if (!resource.HasValue) return "Street (any resource)";
            switch (resource.Value)
            {
                case Resource.Asphalt: return "Street";
                case Resource.Concrete: return "Avenue";
                case Resource.Steel: return "Bridge";
                case Resource.Gravel: return "Country Road";
                default: return "Road";
            }
        }

        public static string Road(Game game, Road road, int viewer, bool viewerIsBot, bool humanToAct)
        {
            string kind = RoadKindLabel(road.Resource);
            string resource = road.Resource.HasValue ? Palette.CardTitle(road.Resource.Value) : LocalizationService.Get("game.ui.050");
            string text = EnglishUi ? $"<b>{DisplayPlace(game.Map.LocationName(road.A))} — {DisplayPlace(game.Map.LocationName(road.B))}</b>\n{kind} · length {road.Length} · {resource} · {game.Config.PointsForLength(road.Length)} pts" : $"<b>{game.Map.LocationName(road.A)} — {game.Map.LocationName(road.B)}</b>\n{char.ToUpper(kind[0]) + kind.Substring(1)} · длина {road.Length} · {resource} · {game.Config.PointsForLength(road.Length)} очк.";

            int? owner = game.GetRoadOwner(road.Id);
            if (!owner.HasValue && road.Id == game.FinalOrderRoadId)
                text += EnglishUi ? $"\n<color=#FFCC40>Final City Hall order: +{game.Config.FinalOrderBonus} pts to whoever builds it in the last round</color>" : $"\n<color=#FFCC40>Финальный заказ мэрии: +{game.Config.FinalOrderBonus} очков тому, кто построит её в последнем круге</color>";
            if (owner.HasValue)
            {
                text += (EnglishUi ? "\nBuilt by: " : "\nПостроил: ") + PlayerIdentity.Label(owner.Value, game.Players[owner.Value].Name);
                if (game.IsRushHourRoad(road.Id))
                    text += EnglishUi ? $"\n<color=#FF9A33>Busy highway (rush hour): toll ×{game.Config.RushHourTollMultiplier}</color>" : $"\n<color=#FF9A33>Загруженная магистраль (час пик): проезд ×{game.Config.RushHourTollMultiplier}</color>";
                int traffic = game.TrafficOn(road.Id);
                if (traffic > 0) text += EnglishUi ? $"\nTraffic: {traffic} rival routes" : $"\nЗагрузка: {traffic} {Plural(traffic, "маршрут", "маршрута", "маршрутов")} соперников";
                if (game.IsJammed(road.Id))
                    text += EnglishUi ? $"\n<color=#FF6B5B>Traffic jam: +{game.Config.JamFee} pts paid to the city. The owner can upgrade the road to a highway.</color>" : $"\n<color=#FF6B5B>Пробка: проезд ещё +{game.Config.JamFee} очк. городу. Владелец может улучшить дорогу до магистрали.</color>";
                else if (game.Config.JamThreshold > 0 && !game.IsRushHourRoad(road.Id) && traffic == game.Config.JamThreshold - 1)
                    text += EnglishUi ? "\n<color=#FFB35B>One more route will cause a traffic jam</color>" : "\n<color=#FFB35B>Ещё один маршрут — и здесь пробка</color>";
                if (owner.Value != viewer && !viewerIsBot)
                {
                    if (game.HasPass(viewer, owner.Value)) text += EnglishUi ? "\nYour road pass makes this toll free" : "\nУ вас пропуск: проезд владельцу бесплатно";
                    else text += EnglishUi ? $"\nYour toll: −{game.TollFor(road.Id, viewer)} pts to the owner" : $"\nПроезд для вас: −{game.TollFor(road.Id, viewer)} очк. владельцу";
                    if (game.IsJammed(road.Id)) text += EnglishUi ? $" and −{game.JamFeeFor(road.Id)} to the city" : $" и −{game.JamFeeFor(road.Id)} городу";
                }
                return text;
            }
            if (game.IsScheduledRepair(road.Id)) text += EnglishUi ? "\n<color=#FF6B6B>Repair Year: closed this round (City Hall permit allows construction)</color>" : "\n<color=#FF6B6B>Год ремонта: закрыт в этом круге (разрешение мэрии пускает строить)</color>";
            else if (game.IsUnderRepair(road.Id)) text += EnglishUi ? "\n<color=#FF6B6B>Roadworks: closed until end of round</color>" : "\n<color=#FF6B6B>Ремонт: закрыт до конца круга</color>";
            if (!owner.HasValue && game.IsNextRepair(road.Id)) text += EnglishUi ? "\n<color=#FF9A33>Repair Year: road closes next round — build it now</color>" : "\n<color=#FF9A33>Год ремонта: закроют в следующем круге — успейте построить</color>";
            if (game.IsParadeClosed(road.Id)) text += EnglishUi ? "\n<color=#CC80FF>Parade: closed until end of round</color>" : "\n<color=#CC80FF>Парад: закрыт до конца круга</color>";
            if (road.Resource == Resource.Gravel && game.CurrentEvent?.Kind == CityEventKind.Snow)
                text += EnglishUi ? $"\n<color=#D9EBFF>Snowfall: +{game.Config.SnowExtraCost} cards to build cost</color>" : $"\n<color=#D9EBFF>Снегопад: +{game.Config.SnowExtraCost} карта к цене</color>";
            if (game.CurrentEvent?.Kind == CityEventKind.Investor)
                text += EnglishUi ? $"\n<color=#FFD94D>Investor: +{game.Config.InvestorBonus} pts for construction</color>" : $"\n<color=#FFD94D>Инвестор: +{game.Config.InvestorBonus} очка за постройку</color>";
            int cost = game.BuildCost(road);
            if (cost < road.Length) text += EnglishUi ? $"\n<color=#7FC8FF>City Hall subsidy: now costs {cost} cards</color>" : $"\n<color=#7FC8FF>Субсидия мэрии: сейчас стоит {cost} карт(ы)</color>";
            if (game.CurrentEvent != null && game.CurrentEvent.Kind == CityEventKind.RushHour)
                text += EnglishUi ? "\n<color=#FF9A33>Rush hour: the road built now will charge double tolls</color>" : "\n<color=#FF9A33>Час пик: построенная сейчас дорога станет платной ×2</color>";
            if (humanToAct && game.Phase == GamePhase.AwaitingAction)
            {
                bool free = game.CanBuildRoadFree(road.Id);
                var ways = game.GetPaymentOptions(road.Id);
                if (free) text += EnglishUi ? "\n<color=#66E7FF>Free construction available</color>" : "\n<color=#66E7FF>Доступно бесплатное строительство</color>";
                if (ways.Count > 0)
                    text += EnglishUi ? $"\n<color=#7FD88A>Preview: {game.BuildCost(road)} cards · {ways.Count} payment options</color>"
                        : $"\n<color=#7FD88A>Предпросмотр: {game.BuildCost(road)} карт · способов оплаты: {ways.Count}</color>";
                string blocker = game.GetBuildBlocker(road.Id);
                return text + "\n" + (blocker != null && !free && ways.Count == 0 ? LocalizationService.TranslateGameError(blocker)
                    : EnglishUi ? "<color=#7FD88A>Click to build</color>" : "<color=#7FD88A>Кликните, чтобы построить</color>");
            }
            return text;
        }

        public static string Player(Game game, int index, string botTag)
        {
            var player = game.Players[index];
            string character = player.Character != CharacterKind.None ? " · " + CharacterLocalization.Title(player.Character) : "";
            return EnglishUi ? $"{player.Name}{character}{botTag}\n{game.VisibleScore(player)} pts · {player.RoadPieces} road pieces · {player.HandSize} cards" + (game.Config.InfluenceEnabled ? $" · {player.Influence} influence" : "") + "\nMore details in the Players panel." : $"{player.Name}{character}{botTag}\n{game.VisibleScore(player)} очков · {player.RoadPieces} участков · {player.HandSize} карт" + (game.Config.InfluenceEnabled ? $" · {player.Influence} влияния" : "") + "\nПодробнее — в панели «Игроки».";
        }

        public static string ResourceCards(Game game, int viewer, bool needsHandoff, Resource resource)
        {
            if (needsHandoff) return LocalizationService.Get("game.tooltip.hand_hidden");
            int count = game.Players[viewer].CardCount(resource);
            string name = EnglishUi ? Palette.CardTitle(resource) : ResourceInfo.RussianName(resource);
            string use = resource == Resource.Machinery ? LocalizationService.Get("game.ui.165") : LocalizationService.Get("game.ui.166") + RoadKindLabel(resource);
            return EnglishUi ? $"{name}: {count} cards\n{use}. Each segment represents one card." : $"{char.ToUpper(name[0]) + name.Substring(1)}: {count} шт.\n{use}. Одно деление — одна карта.";
        }

        public static string Pieces(Game game, int viewerIndex)
        {
            var viewer = game.Players[viewerIndex];
            int start = game.Config.RoadPiecesFor(game.Players.Count);
            return string.Format(LocalizationService.Get("game.tooltip.remaining_roads"), viewer.RoadPieces, start, game.Config.FinalRoundPieces);
        }

        public static string Location(Game game, Location location, int viewerIndex, bool needsHandoff)
        {
            var lines = new List<string> { $"{DisplayPlace(location.Name)} · {DisplayPlace(DistrictName(game.Map, location.DistrictId))}" };
            var roads = game.Map.Roads.Where(r => (r.A == location.Id || r.B == location.Id) && game.IsRoadOpen(r.Id)).ToList();
            int free = roads.Count(r => !game.GetRoadOwner(r.Id).HasValue);
            lines.Add(string.Format(LocalizationService.Get("game.tooltip.location_roads"), roads.Count, free));
            var owners = roads.Select(r => game.GetRoadOwner(r.Id)).Where(o => o.HasValue).GroupBy(o => o.Value)
                .Select(g => $"{game.Players[g.Key].Name} — {g.Count()}").ToList();
            if (owners.Count > 0) lines.Add(LocalizationService.Get("game.tooltip.built_by") + string.Join(", ", owners));
            if (!needsHandoff)
            {
                var viewer = game.Players[viewerIndex];
                foreach (var contract in viewer.Contracts.Where(c => c.Districts.Contains(location.DistrictId)))
                {
                    bool done = game.CheckContract(viewer.Index, contract).Completed;
                    lines.Add(string.Format(LocalizationService.Get("game.tooltip.your_contract"), ContractTitle(game.Map, contract), contract.Points, done ? LocalizationService.Get("game.tooltip.completed") : ""));
                }
            }
            return string.Join("\n", lines);
        }

        public static string ContractTitle(CityMap map, Contract contract)
        {
            string title = $"{DistrictName(map, contract.DistrictA)} — {DistrictName(map, contract.DistrictB)}";
            if (contract.Kind == ContractKind.Transit) title += (EnglishUi ? " via " : " через ") + DistrictName(map, contract.DistrictC);
            else if (contract.Kind == ContractKind.Hub) title += (EnglishUi ? " and " : " и ") + DistrictName(map, contract.DistrictC);
            return title;
        }
    }
}
