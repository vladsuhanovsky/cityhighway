using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// «Песочница правил»: маленькие интерактивные окна поверх страницы правил. Каждое показывает одну механику
    /// на числах из GameConfig, без запуска партии: кликаете — сразу видно, что изменилось и почему.
    /// </summary>
    public static class RuleSandbox
    {
        static bool En => LocalizationService.Language == "en";
        public enum Kind { Toll, Highway, Influence, Deal }

        public static string Title(Kind kind)
        {
            switch (kind)
            {
                case Kind.Toll: return (En ? "Sandbox: contracts and tolls" : "Песочница: контракт и проезд");
                case Kind.Highway: return (En ? "Sandbox: highways and traffic jams" : "Песочница: магистраль и пробки");
                case Kind.Influence: return (En ? "Sandbox: influence" : "Песочница: влияние");
                default: return (En ? "Sandbox: trade" : "Песочница: сделка");
            }
        }

        public static VisualElement Build(Kind kind, GameConfig config)
        {
            switch (kind)
            {
                case Kind.Toll: return Toll(config);
                case Kind.Highway: return Highway(config);
                case Kind.Influence: return Influence(config);
                default: return Deal(config);
            }
        }

        static readonly Color Own = Palette.PlayerColor(0);
        static readonly Color Rival = Palette.PlayerColor(1);
        static readonly Color Free = new Color(1f, 1f, 1f, 0.28f);
        static readonly Color Spot = new Color(0.85f, 0.86f, 0.90f);

        static Label Hint(string text)
        {
            var label = Ui.Text(text, 13f, Theme.TextDim);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 8f;
            return label;
        }

        static VisualElement ResultBox(out Label title, out Label body)
        {
            var box = Ui.Column();
            box.style.marginTop = 10f;
            box.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            Ui.Radius(box, 10f);
            Ui.Padding(box, 10f, 14f);
            box.style.borderLeftWidth = 5f;
            title = Ui.Text("", 16f, Theme.Text, true);
            body = Ui.Text("", 13f, Theme.Text);
            body.style.whiteSpace = WhiteSpace.Normal;
            body.style.marginTop = 2f;
            box.Add(title);
            box.Add(body);
            return box;
        }

        // ================================================================== Контракт и проезд

        /// <summary>
        /// Контракт А — Б и пять дорог двух путей. Клик по дороге: свободна → ваша → чужая → чужая магистраль.
        /// Песочница ищет самый дешёвый засчитываемый путь так же, как игра: хотя бы одна своя дорога,
        /// чужих — не больше предела, плата — за каждую чужую.
        /// </summary>
        static VisualElement Toll(GameConfig config)
        {
            var root = Ui.Column();
            root.Add(Hint((En ? $"Contract A–B. Click roads to cycle them through: unclaimed, yours, rival’s, and rival’s highway. " : $"Контракт «А — Б». Нажимайте на дороги — каждое нажатие меняет их по кругу: свободна, ваша, соперника, магистраль соперника. ") +
                          (En ? $"A route counts if it includes at least one of your roads and at most {config.MaxForeignRoadsPerContract} rival roads. Using rivals’ roads incurs tolls." : $"Засчитывается путь, где есть ваша дорога и не больше {config.MaxForeignRoadsPerContract} чужих; за каждую чужую — проезд.")));
            var board = new SandboxBoard(640f, 220f)
                .Place("a", 0.07f, 0.52f, MapView.FromColor, (En ? "District A" : "Район А"))
                .Place("x", 0.33f, 0.18f, Spot, null)
                .Place("y", 0.66f, 0.18f, Spot, null)
                .Place("z", 0.50f, 0.84f, Spot, null)
                .Place("b", 0.93f, 0.52f, MapView.ToColor, (En ? "District B" : "Район Б"));
            var roads = new[] { board.Road("ax", "a", "x"), board.Road("xy", "x", "y"), board.Road("yb", "y", "b"), board.Road("az", "a", "z"), board.Road("zb", "z", "b") };
            var state = new Dictionary<string, int> { ["ax"] = 1, ["xy"] = 2, ["yb"] = 2, ["az"] = 0, ["zb"] = 0 };
            root.Add(board);
            var result = ResultBox(out var title, out var body);
            root.Add(result);

            void Update()
            {
                foreach (var link in roads)
                {
                    int s = state[link.Id];
                    link.Color = s == 0 ? Free : s == 1 ? Own : Rival;
                    link.Dashed = s == 0;
                    link.Frame = s == 3;
                    link.Highlight = false;
                    int toll = s == 3 ? config.TollPerRoad * config.RushHourTollMultiplier : config.TollPerRoad;
                    link.Label = s == 0 ? (En ? "unclaimed" : "свободна") : s == 1 ? (En ? "yours" : "ваша") : (En ? $"rival −{toll}" : $"чужая −{toll}");
                }
                var paths = new[] { new[] { "ax", "xy", "yb" }, new[] { "az", "zb" } };
                string best = null;
                int bestToll = int.MaxValue;
                string[] bestPath = null;
                var reasons = new List<string>();
                foreach (var path in paths)
                {
                    if (path.Any(id => state[id] == 0))
                    {
                        reasons.Add(path.Length == 3 ? (En ? "upper route incomplete" : "верхний путь не достроен") : (En ? "lower route incomplete" : "нижний путь не достроен"));
                        continue;
                    }
                    int own = path.Count(id => state[id] == 1);
                    int foreign = path.Length - own;
                    if (own == 0)
                    {
                        reasons.Add((path.Length == 3 ? (En ? "on the upper" : "на верхнем") : (En ? "on the lower" : "на нижнем")) + (En ? " route there are no roads of yours" : " пути нет ни одной вашей дороги"));
                        continue;
                    }
                    if (foreign > config.MaxForeignRoadsPerContract)
                    {
                        reasons.Add((path.Length == 3 ? (En ? "on the upper" : "на верхнем") : (En ? "on the lower" : "на нижнем")) + (En ? $" route there are {foreign} rival roads, exceeding {config.MaxForeignRoadsPerContract}" : $" пути {foreign} чужих — больше {config.MaxForeignRoadsPerContract}"));
                        continue;
                    }
                    int toll = path.Sum(id => state[id] == 2 ? config.TollPerRoad : state[id] == 3 ? config.TollPerRoad * config.RushHourTollMultiplier : 0);
                    if (toll < bestToll)
                    {
                        bestToll = toll;
                        bestPath = path;
                        best = path.Length == 3 ? (En ? "upper route" : "верхний путь") : (En ? "lower route" : "нижний путь");
                    }
                }
                if (bestPath != null)
                {
                    foreach (var link in roads) link.Highlight = bestPath.Contains(link.Id);
                    title.text = (En ? "Contract completed" : "Контракт выполнен");
                    title.style.color = GameColors.Success;
                    result.style.borderLeftColor = GameColors.Success;
                    body.text = bestToll == 0
                        ? (En ? $"The {best} counts: all roads are yours, so no tolls are due." : $"Засчитан {best} — только ваши дороги, платить никому не нужно.")
                        : (En ? $"The {best} counts: pay {bestToll} points to rival road owners. The game locks in the cheapest route, " : $"Засчитан {best}: проезд {bestToll} очк. владельцу чужих дорог. Игра сама выбирает самый дешёвый путь и закрепляет его — ") +
                          (En ? "so future rival upgrades cannot increase its cost." : "если соперник потом улучшит дорогу, вы уже не переплатите.");
                }
                else
                {
                    title.text = (En ? "Contract not yet completed" : "Контракт пока не выполнен");
                    title.style.color = GameColors.Danger;
                    result.style.borderLeftColor = GameColors.Danger;
                    body.text = string.Join("; ", reasons) + ".";
                }
                board.Refresh();
            }

            board.Clicked += link =>
            {
                state[link.Id] = (state[link.Id] + 1) % 4;
                Ui.Changed?.Invoke();
                Update();
            };
            Update();
            return root;
        }

        // ================================================================== Магистраль и пробки

        static VisualElement Highway(GameConfig config)
        {
            var root = Ui.Column();
            root.Add(Hint((En ? "This is your road and the rivals using it. Change its length, upgrade to a highway, and add rival contracts. " : "Своя дорога и соперники, которые по ней едут. Меняйте длину, улучшайте до магистрали и добавляйте контракты соперников — ") +
                          (En ? "On the right, see how many points you earn and how many are lost to jams." : "справа видно, сколько получаете вы и сколько сгорает в пробке.")));
            int length = Mathf.Min(3, config.MaxRoadLength);
            bool upgraded = false;
            int traffic = 1;

            var row = Ui.Row();
            row.style.alignItems = Align.FlexStart;
            var board = new SandboxBoard(330f, 170f)
                .Place("a", 0.12f, 0.55f, Spot, null)
                .Place("b", 0.88f, 0.55f, Spot, null);
            var road = board.Road("r", "a", "b");
            row.Add(board);

            var controls = Ui.Column();
            controls.style.marginLeft = 14f;
            controls.style.flexGrow = 1f;
            var lengthRow = Ui.Row();
            lengthRow.style.alignItems = Align.Center;
            lengthRow.Add(Ui.Text((En ? "Length:" : "Длина:"), 14f, Theme.Text, true));
            var lengthButtons = new List<UnityEngine.UIElements.Button>();
            var trafficLabel = Ui.Number("", 18f, Theme.Text, true);
            var upgradeButton = Ui.Button("", () => { }, ButtonKind.Primary, 14f);
            var result = ResultBox(out var title, out var body);

            void Update()
            {
                for (int i = 0; i < lengthButtons.Count; i++)
                {
                    bool active = i + 1 == length;
                    lengthButtons[i].style.backgroundColor = active ? GameColors.Accent : GameColors.Button;
                    lengthButtons[i].style.color = active ? GameColors.AccentText : Theme.Text;
                }
                trafficLabel.text = traffic.ToString();
                int cost = (length + 1) / 2;
                upgradeButton.text = En ? (upgraded ? "Remove upgrade" : $"Upgrade · {cost} cards") : (upgraded ? "Снять улучшение" : $"Улучшить · {cost} карт{(cost == 1 ? "а" : "ы")}");
                bool jammed = !upgraded && config.JamThreshold > 0 && traffic >= config.JamThreshold;
                road.Color = Own;
                road.Dashed = false;
                road.Frame = upgraded;
                road.Label = upgraded ? (En ? "highway" : "магистраль") : jammed ? (En ? "jam!" : "пробка!") : (En ? $"regular · traffic {traffic}" : $"обычная · загрузка {traffic}");
                board.Refresh();

                int toll = config.TollPerRoad * (upgraded ? config.RushHourTollMultiplier : 1);
                int income = toll * traffic;
                // Пробочный сбор: проезд, перед которым загрузка уже на пороге, платит ещё JamFee городу.
                int burned = 0;
                if (!upgraded && config.JamThreshold > 0)
                    for (int before = 0; before < traffic; before++)
                        if (before >= config.JamThreshold) burned += config.JamFee;
                int points = config.PointsForLength(length) + (upgraded ? config.UpgradePoints : 0);
                title.text = (En ? $"Road: +{points} pts · toll income: +{income}" : $"Дорога: +{points} очк. · проезд: +{income} вам");
                title.style.color = upgraded ? GameColors.Accent : GameColors.Success;
                result.style.borderLeftColor = jammed ? GameColors.Danger : upgraded ? GameColors.Accent : GameColors.Success;
                body.text = (En ? $"Length {length}: {config.PointsForLength(length)} points." : $"За длину {length}: {config.PointsForLength(length)} очк.") + (upgraded ? (En ? $" Highway: +{config.UpgradePoints} more." : $" Магистраль: ещё +{config.UpgradePoints}.") : "") +
                            (En ? $" Each of {traffic} rival contracts pays you {toll} points." : $" Каждый из {traffic} соперничьих контрактов платит вам {toll}.") +
                            (burned > 0 ? (En ? $" Jam: rivals lose an additional {burned} points, which are not paid to you. Upgrade the road or wait for a detour." : $" Пробка: соперники дополнительно теряют {burned} очк. — они сгорают, вам не достаются. Улучшите дорогу или ждите объезда.")
                                : jammed ? (En ? " Jammed road: the next crossing costs rivals more." : " Дорога в пробке: следующий проезд будет стоить соперникам дороже.")
                                : upgraded ? (En ? " Highways cannot become jammed." : " Магистраль в пробку не встаёт.") : "");
            }

            for (int l = 1; l <= config.MaxRoadLength; l++)
            {
                int value = l;
                var button = Ui.Button(value.ToString(), () => { length = value; Ui.Changed?.Invoke(); Update(); }, ButtonKind.Secondary, 14f);
                button.style.width = 40f;
                button.style.marginLeft = 6f;
                lengthButtons.Add(button);
                lengthRow.Add(button);
            }
            controls.Add(lengthRow);

            var trafficRow = Ui.Row();
            trafficRow.style.alignItems = Align.Center;
            trafficRow.style.marginTop = 10f;
            trafficRow.Add(Ui.Text((En ? "Rival contracts on this road:" : "Контрактов соперников по ней:"), 14f, Theme.Text, true));
            var less = Ui.Button("–", () => { traffic = Mathf.Max(0, traffic - 1); Ui.Changed?.Invoke(); Update(); }, ButtonKind.Secondary, 16f);
            less.style.width = 40f;
            less.style.marginLeft = 8f;
            trafficRow.Add(less);
            trafficLabel.style.width = 36f;
            trafficLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            trafficRow.Add(trafficLabel);
            var more = Ui.Button("+", () => { traffic = Mathf.Min(6, traffic + 1); Ui.Changed?.Invoke(); Update(); }, ButtonKind.Secondary, 16f);
            more.style.width = 40f;
            trafficRow.Add(more);
            controls.Add(trafficRow);

            upgradeButton.clicked += () =>
            {
                upgraded = !upgraded;
                Update();
            };
            upgradeButton.style.marginTop = 12f;
            upgradeButton.style.alignSelf = Align.FlexStart;
            controls.Add(upgradeButton);
            controls.Add(Hint((En ? $"An upgrade takes a turn and cards of the road’s resource equal to half its length (rounded up). Jams start at traffic {config.JamThreshold}." : $"Улучшение — это ход и половина длины дороги в картах её ресурса (с округлением вверх). Пробка начинается с загрузки {config.JamThreshold}.")));
            row.Add(controls);
            root.Add(row);
            root.Add(result);
            Update();
            return root;
        }

        // ================================================================== Влияние

        static VisualElement Influence(GameConfig config)
        {
            var root = Ui.Column();
            root.Add(Hint((En ? "Earn and spend influence, at most once per turn before the main action. " : "Копите влияние и тратьте его: не больше одного раза за ход, до основного действия. ") +
                          (En ? "This example has a snowstorm and five face-up cards." : "Здесь идёт снегопад, а в открытом ряду — пять карт.")));
            int influence = 6, turn = 1, refreshes = 0;
            bool usedThisTurn = false, permit = false, snow = true;
            var random = new System.Random(7);
            var market = new Resource[config.FaceUpSlots > 0 ? config.FaceUpSlots : 5];
            void DealMarket() { for (int i = 0; i < market.Length; i++) market[i] = (Resource)random.Next(0, ResourceInfo.Count); }
            DealMarket();

            var top = Ui.Row();
            top.style.alignItems = Align.Center;
            var wallet = Ui.Number("", 30f, new Color(0.75f, 0.60f, 0.98f), true);
            top.Add(wallet);
            var turnLabel = Ui.Text("", 14f, Theme.TextDim, true);
            turnLabel.style.marginLeft = 14f;
            turnLabel.style.flexGrow = 1f;
            top.Add(turnLabel);
            var eventChip = Ui.Text("", 13f, Color.white, true);
            Ui.Radius(eventChip, 8f);
            Ui.Padding(eventChip, 4f, 10f);
            top.Add(eventChip);
            root.Add(top);

            var marketRow = Ui.Row();
            marketRow.style.marginTop = 8f;
            var cards = new List<CardView>();
            for (int i = 0; i < market.Length; i++)
            {
                var card = new CardView(56f, 78f, false);
                card.style.marginRight = 8f;
                Ui.Transitions(card, 0.2f, "opacity", "translate");
                cards.Add(card);
                marketRow.Add(card);
            }
            root.Add(marketRow);

            var actions = Ui.Row();
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 10f;
            root.Add(actions);
            var income = Ui.Row();
            income.style.marginTop = 4f;
            income.style.flexWrap = Wrap.Wrap;
            root.Add(income);
            var log = Ui.Column();
            log.style.marginTop = 8f;
            log.style.minHeight = 96f;
            root.Add(log);
            var buttons = new Dictionary<InfluenceAction, UnityEngine.UIElements.Button>();

            int CostOf(InfluenceAction action) =>
                InfluenceInfo.Cost(action, config) + (action == InfluenceAction.RefreshMarket ? refreshes * config.InfluenceRefreshStep : 0);

            void Write(string text, Color color)
            {
                var line = Ui.Text(text, 13f, color);
                line.style.whiteSpace = WhiteSpace.Normal;
                log.Insert(0, line);
                Ui.Appear(line);
                while (log.childCount > 4) log.RemoveAt(log.childCount - 1);
            }

            void Update()
            {
                wallet.text = (En ? $"{influence} influence" : $"{influence} вл.");
                turnLabel.text = (En ? $"Turn {turn}" : $"Ход {turn}") + (usedThisTurn ? (En ? " · influence already spent this turn" : " · влияние в этот ход уже потрачено") : (En ? " · you may spend influence" : " · можно потратить"));
                eventChip.text = snow ? (permit ? (En ? "Snowfall (you have a permit)" : "Снегопад (у вас разрешение)") : (En ? "Snowfall: gravel costs more" : "Снегопад: щебень дороже")) : (En ? "No events" : "Событий нет");
                eventChip.style.backgroundColor = snow ? GameView.EventColor(CityEventKind.Snow) : GameColors.Button;
                for (int i = 0; i < cards.Count; i++) cards[i].Set(market[i]);
                foreach (var pair in buttons)
                {
                    int cost = CostOf(pair.Key);
                    pair.Value.text = $"{RulesView.RuleInfluenceTitle(pair.Key)} · {cost}";
                    bool possible = !usedThisTurn && influence >= cost && (pair.Key != InfluenceAction.CancelEvent || snow);
                    Ui.SetEnabled(pair.Value, possible);
                }
            }

            foreach (var action in InfluenceInfo.All)
            {
                var chosen = action;
                var button = Ui.Button("", () =>
                {
                    int cost = CostOf(chosen);
                    influence -= cost;
                    usedThisTurn = true;
                    switch (chosen)
                    {
                        case InfluenceAction.RefreshMarket:
                            refreshes++;
                            DealMarket();
                            foreach (var card in cards) Ui.Appear(card);
                            Write((En ? $"−{cost}: the row was discarded and replaced. Your next refresh costs {config.InfluenceRefreshStep} more." : $"−{cost}: ряд ушёл в сброс, выложены новые карты. Следующее ваше обновление дороже на {config.InfluenceRefreshStep}."), Theme.Text);
                            break;
                        case InfluenceAction.ExtraContracts:
                            Write((En ? $"−{cost}: draw {config.ContractsDrawn + 1} contracts instead of {config.ContractsDrawn}; this consumes your turn." : $"−{cost}: вы берёте {config.ContractsDrawn + 1} контракта вместо {config.ContractsDrawn} — это и есть действие хода."), Theme.Text);
                            break;
                        case InfluenceAction.Permit:
                            permit = true;
                            Write((En ? $"−{cost}: City Hall permit protects you from snow and parade effects this turn." : $"−{cost}: разрешение мэрии — в этот ход снег и парад на вас не действуют."), Theme.Text);
                            break;
                        default:
                            snow = false;
                            Write((En ? $"−{cost}: the event was cancelled for everyone." : $"−{cost}: событие отменено для всех."), Theme.Text);
                            break;
                    }
                    Update();
                }, ButtonKind.Secondary, 13f);
                button.style.marginRight = 8f;
                button.style.marginBottom = 6f;
                buttons[action] = button;
                actions.Add(button);
            }
            void Income(string caption, int amount, string text)
            {
                var button = Ui.Button($"{caption} +{amount}", () =>
                {
                    influence += amount;
                    Write($"+{amount}: {text}", GameColors.Success);
                    Update();
                }, ButtonKind.Ghost, 13f);
                button.style.marginRight = 8f;
                button.style.marginBottom = 6f;
                income.Add(button);
            }
            Income((En ? "Contract completed" : "Контракт выполнен"), config.InfluencePerContract, (En ? "City Hall rewards your completed contract." : "мэрия довольна выполненным контрактом."));
            Income((En ? "New district reached" : "Новый район в сети"), config.InfluencePerNewDistrict, (En ? "your network expanded to a new district." : "ваша сеть дотянулась до нового района."));
            var next = Ui.Button((En ? "Next turn" : "Следующий ход"), () =>
            {
                turn++;
                usedThisTurn = false;
                permit = false;
                Write((En ? $"Turn {turn}: you can spend influence once again." : $"Ход {turn}: влияние снова можно потратить один раз."), Theme.TextDim);
                Update();
            }, ButtonKind.Primary, 13f);
            next.style.marginBottom = 6f;
            income.Add(next);
            Update();
            return root;
        }

        // ================================================================== Сделка

        static VisualElement Deal(GameConfig config)
        {
            var root = Ui.Column();
            root.Add(Hint((En ? $"Offer a rival 1–{config.TradeMaxCards} resource cards for another resource, optionally adding influence " : $"Предложите сопернику обмен: от 1 до {config.TradeMaxCards} карт одного ресурса на карты другого, можно доплатить влиянием ") +
                          (En ? $"(up to {config.TradeMaxInfluence}). In this sandbox, the rival accepts if they receive at least as much as they give. A deal does not consume your turn." : $"(до {config.TradeMaxInfluence}). Соперник в песочнице соглашается, если получает не меньше, чем отдаёт. Сделка хода не тратит.")));
            var kinds = ResourceInfo.Building;
            var mine = new Dictionary<Resource, int> { [Resource.Asphalt] = 4, [Resource.Concrete] = 1, [Resource.Steel] = 0, [Resource.Gravel] = 3 };
            var theirs = new Dictionary<Resource, int> { [Resource.Asphalt] = 0, [Resource.Concrete] = 3, [Resource.Steel] = 2, [Resource.Gravel] = 1 };
            int myInfluence = 4;
            int give = 0, giveCount = 2, get = 2, getCount = 1, pay = 0;
            bool done = false;

            VisualElement Hand(string caption, Dictionary<Resource, int> hand, out Action refresh)
            {
                var column = Ui.Column();
                column.style.flexGrow = 1f;
                column.style.flexBasis = 0f;
                column.Add(Ui.Text(caption, 14f, Theme.TextDim, true));
                var row = Ui.Row();
                row.style.marginTop = 4f;
                var views = new List<CardView>();
                foreach (var resource in kinds)
                {
                    var card = new CardView(54f, 76f, true);
                    card.style.marginRight = 6f;
                    views.Add(card);
                    row.Add(card);
                }
                column.Add(row);
                refresh = () =>
                {
                    for (int i = 0; i < kinds.Length; i++) views[i].Set(kinds[i], hand[kinds[i]], hand[kinds[i]] == 0);
                };
                return column;
            }

            var hands = Ui.Row();
            hands.Add(Hand((En ? "Your hand" : "Ваша рука"), mine, out var refreshMine));
            hands.Add(Hand((En ? "Rival hand" : "Рука соперника"), theirs, out var refreshTheirs));
            root.Add(hands);

            var offer = Ui.Row();
            offer.style.alignItems = Align.Center;
            offer.style.flexWrap = Wrap.Wrap;
            offer.style.marginTop = 12f;
            var giveButton = Ui.Button("", () => { }, ButtonKind.Secondary, 13f);
            var getButton = Ui.Button("", () => { }, ButtonKind.Secondary, 13f);
            var payButton = Ui.Button("", () => { }, ButtonKind.Secondary, 13f);
            var propose = Ui.Button((En ? "Propose" : "Предложить"), () => { }, ButtonKind.Primary, 14f);
            var result = ResultBox(out var title, out var body);
            var walletLabel = Ui.Text("", 13f, new Color(0.75f, 0.60f, 0.98f), true);

            void Update()
            {
                giveButton.text = (En ? $"Offering: {giveCount} × {Palette.CardTitle(kinds[give])}" : $"Отдаю: {giveCount} × {Palette.CardTitle(kinds[give])}");
                getButton.text = (En ? $"Requesting: {getCount} × {Palette.CardTitle(kinds[get])}" : $"Прошу: {getCount} × {Palette.CardTitle(kinds[get])}");
                payButton.text = pay > 0 ? (En ? $"Payment: {pay} influence" : $"Доплата: {pay} вл.") : (En ? "No payment" : "Без доплаты");
                walletLabel.text = (En ? $"Your influence: {myInfluence}" : $"Ваше влияние: {myInfluence}");
                refreshMine();
                refreshTheirs();
                Ui.SetEnabled(propose, !done);
            }

            giveButton.clicked += () =>
            {
                // Клик листает «сколько», после максимума — следующий ресурс.
                giveCount++;
                if (giveCount > config.TradeMaxCards) { giveCount = 1; give = (give + 1) % kinds.Length; if (give == get) give = (give + 1) % kinds.Length; }
                Update();
            };
            getButton.clicked += () =>
            {
                getCount++;
                if (getCount > config.TradeMaxCards) { getCount = 1; get = (get + 1) % kinds.Length; if (get == give) get = (get + 1) % kinds.Length; }
                Update();
            };
            payButton.clicked += () =>
            {
                pay = (pay + 1) % (Mathf.Min(config.TradeMaxInfluence, 3) + 1);
                Update();
            };
            propose.clicked += () =>
            {
                var g = kinds[give];
                var r = kinds[get];
                if (mine[g] < giveCount) Verdict(false, (En ? $"You have only {mine[g]} × {Palette.CardTitle(g)}, so you cannot give {giveCount}." : $"У вас только {mine[g]} × {Palette.CardTitle(g)} — нельзя отдать {giveCount}."));
                else if (theirs[r] < getCount) Verdict(false, (En ? $"Your rival has only {theirs[r]} × {Palette.CardTitle(r)}." : $"У соперника только {theirs[r]} × {Palette.CardTitle(r)}."));
                else if (myInfluence < pay) Verdict(false, (En ? "Not enough influence for the payment." : "Не хватает влияния на доплату."));
                else if (giveCount + pay * 0.5f < getCount) Verdict(false, (En ? $"Rival declined: receives {giveCount} cards" : $"Соперник отказался: получает {giveCount} карт") + (pay > 0 ? (En ? $" and {pay} influence" : $" и {pay} вл.") : "") + (En ? $", but gives up {getCount}. Add cards or influence." : $", а отдаёт {getCount}. Добавьте карту или влияние."));
                else
                {
                    mine[g] -= giveCount; theirs[g] += giveCount;
                    theirs[r] -= getCount; mine[r] += getCount;
                    myInfluence -= pay;
                    done = true;
                    Verdict(true, (En ? $"Deal accepted: you gave {giveCount} × {Palette.CardTitle(g)}" : $"Сделка заключена: вы отдали {giveCount} × {Palette.CardTitle(g)}") + (pay > 0 ? (En ? $" and {pay} influence" : $" и {pay} вл.") : "") +
                                  (En ? $", received {getCount} × {Palette.CardTitle(r)}. Only one deal per turn; you may still build." : $", получили {getCount} × {Palette.CardTitle(r)}. Сделка — одна за ход, и ход ещё ваш: можно строить."));
                }
                Update();
            };

            void Verdict(bool ok, string text)
            {
                title.text = ok ? (En ? "Rival accepted" : "Соперник согласился") : (En ? "Deal failed" : "Сделка не состоялась");
                title.style.color = ok ? GameColors.Success : GameColors.Danger;
                result.style.borderLeftColor = ok ? GameColors.Success : GameColors.Danger;
                body.text = text;
            }

            foreach (var button in new[] { giveButton, getButton, payButton })
            {
                button.style.marginRight = 8f;
                button.style.marginBottom = 6f;
                offer.Add(button);
            }
            propose.style.marginBottom = 6f;
            offer.Add(propose);
            root.Add(offer);
            var bottom = Ui.Row();
            bottom.style.alignItems = Align.Center;
            bottom.Add(walletLabel);
            var space = Ui.Box();
            space.style.flexGrow = 1f;
            bottom.Add(space);
            bottom.Add(Ui.Button((En ? "New turn" : "Новый ход"), () =>
            {
                done = false;
                title.text = "";
                body.text = (En ? "New turn: you may propose another deal." : "Новый ход — можно снова предложить одну сделку.");
                result.style.borderLeftColor = GameColors.Button;
                Update();
            }, ButtonKind.Ghost, 13f));
            root.Add(bottom);
            title.text = "";
            body.text = (En ? "Click 'Offering' and 'Requesting' to choose card counts and resource types." : "Кликайте по кнопкам «Отдаю» и «Прошу», чтобы выбрать количество и ресурс.");
            result.style.borderLeftColor = GameColors.Button;
            root.Add(result);
            Update();
            return root;
        }
    }
}
