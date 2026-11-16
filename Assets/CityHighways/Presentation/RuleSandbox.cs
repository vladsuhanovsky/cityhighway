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
        public enum Kind { Toll, Highway, Influence, Deal }

        public static string Title(Kind kind)
        {
            switch (kind)
            {
                case Kind.Toll: return "Песочница: контракт и проезд";
                case Kind.Highway: return "Песочница: магистраль и пробки";
                case Kind.Influence: return "Песочница: влияние";
                default: return "Песочница: сделка";
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
            root.Add(Hint($"Контракт «А — Б». Нажимайте на дороги — каждое нажатие меняет их по кругу: свободна, ваша, соперника, магистраль соперника. " +
                          $"Засчитывается путь, где есть ваша дорога и не больше {config.MaxForeignRoadsPerContract} чужих; за каждую чужую — проезд."));
            var board = new SandboxBoard(640f, 220f)
                .Place("a", 0.07f, 0.52f, MapView.FromColor, "Район А")
                .Place("x", 0.33f, 0.18f, Spot, null)
                .Place("y", 0.66f, 0.18f, Spot, null)
                .Place("z", 0.50f, 0.84f, Spot, null)
                .Place("b", 0.93f, 0.52f, MapView.ToColor, "Район Б");
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
                    link.Label = s == 0 ? "свободна" : s == 1 ? "ваша" : $"чужая −{toll}";
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
                        reasons.Add(path.Length == 3 ? "верхний путь не достроен" : "нижний путь не достроен");
                        continue;
                    }
                    int own = path.Count(id => state[id] == 1);
                    int foreign = path.Length - own;
                    if (own == 0)
                    {
                        reasons.Add((path.Length == 3 ? "на верхнем" : "на нижнем") + " пути нет ни одной вашей дороги");
                        continue;
                    }
                    if (foreign > config.MaxForeignRoadsPerContract)
                    {
                        reasons.Add((path.Length == 3 ? "на верхнем" : "на нижнем") + $" пути {foreign} чужих — больше {config.MaxForeignRoadsPerContract}");
                        continue;
                    }
                    int toll = path.Sum(id => state[id] == 2 ? config.TollPerRoad : state[id] == 3 ? config.TollPerRoad * config.RushHourTollMultiplier : 0);
                    if (toll < bestToll)
                    {
                        bestToll = toll;
                        bestPath = path;
                        best = path.Length == 3 ? "верхний путь" : "нижний путь";
                    }
                }
                if (bestPath != null)
                {
                    foreach (var link in roads) link.Highlight = bestPath.Contains(link.Id);
                    title.text = "Контракт выполнен";
                    title.style.color = GameColors.Success;
                    result.style.borderLeftColor = GameColors.Success;
                    body.text = bestToll == 0
                        ? $"Засчитан {best} — только ваши дороги, платить никому не нужно."
                        : $"Засчитан {best}: проезд {bestToll} очк. владельцу чужих дорог. Игра сама выбирает самый дешёвый путь и закрепляет его — " +
                          "если соперник потом улучшит дорогу, вы уже не переплатите.";
                }
                else
                {
                    title.text = "Контракт пока не выполнен";
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
            root.Add(Hint("Своя дорога и соперники, которые по ней едут. Меняйте длину, улучшайте до магистрали и добавляйте контракты соперников — " +
                          "справа видно, сколько получаете вы и сколько сгорает в пробке."));
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
            lengthRow.Add(Ui.Text("Длина:", 14f, Theme.Text, true));
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
                upgradeButton.text = upgraded ? "Снять улучшение" : $"Улучшить · {cost} карт{(cost == 1 ? "а" : "ы")}";
                bool jammed = !upgraded && config.JamThreshold > 0 && traffic >= config.JamThreshold;
                road.Color = Own;
                road.Dashed = false;
                road.Frame = upgraded;
                road.Label = upgraded ? "магистраль" : jammed ? "пробка!" : $"обычная · загрузка {traffic}";
                board.Refresh();

                int toll = config.TollPerRoad * (upgraded ? config.RushHourTollMultiplier : 1);
                int income = toll * traffic;
                // Пробочный сбор: проезд, перед которым загрузка уже на пороге, платит ещё JamFee городу.
                int burned = 0;
                if (!upgraded && config.JamThreshold > 0)
                    for (int before = 0; before < traffic; before++)
                        if (before >= config.JamThreshold) burned += config.JamFee;
                int points = config.PointsForLength(length) + (upgraded ? config.UpgradePoints : 0);
                title.text = $"Дорога: +{points} очк. · проезд: +{income} вам";
                title.style.color = upgraded ? GameColors.Accent : GameColors.Success;
                result.style.borderLeftColor = jammed ? GameColors.Danger : upgraded ? GameColors.Accent : GameColors.Success;
                body.text = $"За длину {length}: {config.PointsForLength(length)} очк." + (upgraded ? $" Магистраль: ещё +{config.UpgradePoints}." : "") +
                            $" Каждый из {traffic} соперничьих контрактов платит вам {toll}." +
                            (burned > 0 ? $" Пробка: соперники дополнительно теряют {burned} очк. — они сгорают, вам не достаются. Улучшите дорогу или ждите объезда."
                                : jammed ? " Дорога в пробке: следующий проезд будет стоить соперникам дороже."
                                : upgraded ? " Магистраль в пробку не встаёт." : "");
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
            trafficRow.Add(Ui.Text("Контрактов соперников по ней:", 14f, Theme.Text, true));
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
            controls.Add(Hint($"Улучшение — это ход и половина длины дороги в картах её ресурса (с округлением вверх). Пробка начинается с загрузки {config.JamThreshold}."));
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
            root.Add(Hint("Копите влияние и тратьте его: не больше одного раза за ход, до основного действия. " +
                          "Здесь идёт снегопад, а в открытом ряду — пять карт."));
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
                wallet.text = $"{influence} вл.";
                turnLabel.text = $"Ход {turn}" + (usedThisTurn ? " · влияние в этот ход уже потрачено" : " · можно потратить");
                eventChip.text = snow ? (permit ? "Снегопад (у вас разрешение)" : "Снегопад: щебень дороже") : "Событий нет";
                eventChip.style.backgroundColor = snow ? GameView.EventColor(CityEventKind.Snow) : GameColors.Button;
                for (int i = 0; i < cards.Count; i++) cards[i].Set(market[i]);
                foreach (var pair in buttons)
                {
                    int cost = CostOf(pair.Key);
                    pair.Value.text = $"{InfluenceInfo.Title(pair.Key)} · {cost}";
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
                            Write($"−{cost}: ряд ушёл в сброс, выложены новые карты. Следующее ваше обновление дороже на {config.InfluenceRefreshStep}.", Theme.Text);
                            break;
                        case InfluenceAction.ExtraContracts:
                            Write($"−{cost}: вы берёте {config.ContractsDrawn + 1} контракта вместо {config.ContractsDrawn} — это и есть действие хода.", Theme.Text);
                            break;
                        case InfluenceAction.Permit:
                            permit = true;
                            Write($"−{cost}: разрешение мэрии — в этот ход снег и парад на вас не действуют.", Theme.Text);
                            break;
                        default:
                            snow = false;
                            Write($"−{cost}: событие отменено для всех.", Theme.Text);
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
            Income("Контракт выполнен", config.InfluencePerContract, "мэрия довольна выполненным контрактом.");
            Income("Новый район в сети", config.InfluencePerNewDistrict, "ваша сеть дотянулась до нового района.");
            var next = Ui.Button("Следующий ход", () =>
            {
                turn++;
                usedThisTurn = false;
                permit = false;
                Write($"Ход {turn}: влияние снова можно потратить один раз.", Theme.TextDim);
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
            root.Add(Hint($"Предложите сопернику обмен: от 1 до {config.TradeMaxCards} карт одного ресурса на карты другого, можно доплатить влиянием " +
                          $"(до {config.TradeMaxInfluence}). Соперник в песочнице соглашается, если получает не меньше, чем отдаёт. Сделка хода не тратит."));
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
            hands.Add(Hand("Ваша рука", mine, out var refreshMine));
            hands.Add(Hand("Рука соперника", theirs, out var refreshTheirs));
            root.Add(hands);

            var offer = Ui.Row();
            offer.style.alignItems = Align.Center;
            offer.style.flexWrap = Wrap.Wrap;
            offer.style.marginTop = 12f;
            var giveButton = Ui.Button("", () => { }, ButtonKind.Secondary, 13f);
            var getButton = Ui.Button("", () => { }, ButtonKind.Secondary, 13f);
            var payButton = Ui.Button("", () => { }, ButtonKind.Secondary, 13f);
            var propose = Ui.Button("Предложить", () => { }, ButtonKind.Primary, 14f);
            var result = ResultBox(out var title, out var body);
            var walletLabel = Ui.Text("", 13f, new Color(0.75f, 0.60f, 0.98f), true);

            void Update()
            {
                giveButton.text = $"Отдаю: {giveCount} × {Palette.CardTitle(kinds[give])}";
                getButton.text = $"Прошу: {getCount} × {Palette.CardTitle(kinds[get])}";
                payButton.text = pay > 0 ? $"Доплата: {pay} вл." : "Без доплаты";
                walletLabel.text = $"Ваше влияние: {myInfluence}";
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
                if (mine[g] < giveCount) Verdict(false, $"У вас только {mine[g]} × {Palette.CardTitle(g)} — нельзя отдать {giveCount}.");
                else if (theirs[r] < getCount) Verdict(false, $"У соперника только {theirs[r]} × {Palette.CardTitle(r)}.");
                else if (myInfluence < pay) Verdict(false, "Не хватает влияния на доплату.");
                else if (giveCount + pay * 0.5f < getCount) Verdict(false, $"Соперник отказался: получает {giveCount} карт" + (pay > 0 ? $" и {pay} вл." : "") + $", а отдаёт {getCount}. Добавьте карту или влияние.");
                else
                {
                    mine[g] -= giveCount; theirs[g] += giveCount;
                    theirs[r] -= getCount; mine[r] += getCount;
                    myInfluence -= pay;
                    done = true;
                    Verdict(true, $"Сделка заключена: вы отдали {giveCount} × {Palette.CardTitle(g)}" + (pay > 0 ? $" и {pay} вл." : "") +
                                  $", получили {getCount} × {Palette.CardTitle(r)}. Сделка — одна за ход, и ход ещё ваш: можно строить.");
                }
                Update();
            };

            void Verdict(bool ok, string text)
            {
                title.text = ok ? "Соперник согласился" : "Сделка не состоялась";
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
            bottom.Add(Ui.Button("Новый ход", () =>
            {
                done = false;
                title.text = "";
                body.text = "Новый ход — можно снова предложить одну сделку.";
                result.style.borderLeftColor = GameColors.Button;
                Update();
            }, ButtonKind.Ghost, 13f));
            root.Add(bottom);
            title.text = "";
            body.text = "Кликайте по кнопкам «Отдаю» и «Прошу», чтобы выбрать количество и ресурс.";
            result.style.borderLeftColor = GameColors.Button;
            root.Add(result);
            Update();
            return root;
        }
    }
}
