using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Наставник обучающей партии: панель в углу карты с одним заданием или объяснением за раз.
    /// Задания переключаются сами, когда игрок их выполнил (по состоянию партии), объяснения — по кнопке «Понятно».
    /// Шаг, который уже выполнен к моменту показа, пропускается. Подсказанная дорога подсвечивается на карте.
    /// Игрок в обучении всегда за местом 0.
    /// </summary>
    public sealed class TutorialCoach
    {
        public const string DonePrefsKey = "CityHighways.TutorialGameDone.v1";
        const int Me = 0;

        sealed class Step
        {
            public string Title;
            /// <summary>Текст (может зависеть от партии).</summary>
            public System.Func<string> Text;
            /// <summary>Задание выполнено — шаг пропускается/закрывается. null — объяснение, закрывается кнопкой.</summary>
            public System.Func<bool> Done;
            /// <summary>Когда шаг пора показать (по умолчанию — сразу).</summary>
            public System.Func<bool> When;
            /// <summary>Подсвечивать дорогу для контрактов.</summary>
            public bool HintRoad;
        }

        static bool En => LocalizationService.Language == "en";
        static readonly string[] EnglishStepTitles =
        {
            "Welcome!", "Starting Contracts", "Draw Cards", "Build a Road",
            "Road Points", "Rival Roads", "Complete a Contract", "Contract Completed!",
            "New Contracts", "Final Round Approaching", "Final Round"
        };

        string EnglishResource(Road road) =>
            road.Resource.HasValue ? Palette.CardTitle(road.Resource.Value) : "one resource type";

        string EnglishHintCost()
        {
            var road = HintedRoad();
            if (road == null) return "";
            int cost = _game.BuildCost(road, MePlayer);
            int have = road.Resource.HasValue ? MePlayer.CardCount(road.Resource.Value) : MaxBuilding();
            have += MePlayer.CardCount(Resource.Machinery);
            return $"You need {cost} cards of {EnglishResource(road)}; you have {Mathf.Min(have, cost)} suitable cards.";
        }

        string EnglishNeedText()
        {
            var road = HintedRoad();
            return road == null
                ? "Draw cards matching the resources used by roads leading to your contracts."
                : $"The highlighted blue road needs {_game.BuildCost(road, MePlayer)} cards of {EnglishResource(road)}.";
        }

        string EnglishStepText(int index)
        {
            switch (index)
            {
                case 0: return "This tutorial takes place on a small map in central Moscow. You run a road company against the Mentor. Build roads and complete City Hall contracts to score the most points. Follow the steps to learn.";
                case 1: return "The Contracts panel at the bottom lists districts to connect with your roads. Completed contracts earn points, and unfinished ones lose points at game end. Hover to highlight districts. Keep at least two contracts and choose Take Selected.";
                case 2: return "On your turn, draw two cards from the Actions panel, either face-up or from the deck. " + EnglishNeedText() + " Machinery is a wild card, but taking face-up machinery uses both draws.";
                case 3: return "The blue highlight shows a road leading toward your contract. Click it when you have enough cards: " + EnglishHintCost() + " If you need more cards, draw again. Available roads pulse.";
                case 4: return $"Every completed road scores immediately: length 1 earns {_game.Config.PointsForLength(1)}, length 2 earns {_game.Config.PointsForLength(2)}, and length 3 earns {_game.Config.PointsForLength(3)}. Longer roads pay more. Road pieces remaining: {MePlayer.RoadPieces}.";
                case 5: return $"The Mentor is building too. A rival road is not a dead end: a contract route must contain at least one of your roads and no more than {_game.Config.MaxForeignRoadsPerContract} rival roads. Pay each rival road owner {_game.Config.TollPerRoad} points when completing the contract. Rivals also pay to use your roads.";
                case 6: return "Finish the connection between your contract's districts. The highlighted road is the next segment to build. " + EnglishHintCost();
                case 7: return "Your contract is complete! Its points are awarded at game end, and you've already earned influence. In Actions, spend influence to refresh the card market or draw extra contracts. Remaining influence goes to your profile for cosmetics.";
                case 8: return "All your contracts are complete. While you still have road pieces, draw more contracts using the Actions panel (this consumes a turn). Keep at least one, or build long roads for points.";
                case 9: return $"When anyone has {_game.Config.FinalRoundPieces} road pieces or fewer, the final round begins and every player gets one more turn. Check unfinished contracts: they cost points at game end.";
                case 10: return (_game.FinalOrderRoad != null ? $"City Hall has issued a final order: the gold-outlined road earns +{_game.Config.FinalOrderBonus} points if built this round. " : "") + "This is your last turn. Finish a contract if possible, or build the longest available road.";
                default: return "";
            }
        }

        public readonly VisualElement Panel;
        readonly Label _counter;
        readonly Label _title;
        readonly Label _text;
        readonly UnityEngine.UIElements.Button _ok;
        readonly Game _game;
        readonly MapView _map;
        readonly List<Step> _steps;
        /// <summary>Пройденные шаги: задания — когда выполнены, объяснения — по «Понятно».</summary>
        readonly HashSet<int> _finished = new HashSet<int>();
        int _index = -1;
        bool _hidden;

        public static bool IsDone => PlayerPrefs.GetInt(DonePrefsKey, 0) == 1;

        public static void MarkDone()
        {
            PlayerPrefs.SetInt(DonePrefsKey, 1);
            PlayerPrefs.Save();
        }

        public TutorialCoach(Game game, MapView map)
        {
            _game = game;
            _map = map;
            _steps = BuildSteps();

            Panel = Ui.Card(16f);
            Panel.pickingMode = PickingMode.Position;
            Panel.style.position = Position.Absolute;
            Panel.style.width = 380f;
            Panel.style.backgroundColor = new Color(0.10f, 0.12f, 0.15f, 0.96f);
            Ui.Border(Panel, 2f, Theme.Gold);
            _counter = Ui.Text("", 11f, Theme.TextDim, true);
            Panel.Add(_counter);
            _title = Ui.Text("", 19f, Theme.Gold, true);
            _title.style.marginTop = 2f;
            Panel.Add(_title);
            _text = Ui.Text("", 14f, Theme.Text);
            _text.style.whiteSpace = WhiteSpace.Normal;
            _text.style.marginTop = 6f;
            _text.style.marginBottom = 10f;
            Panel.Add(_text);
            var buttons = Ui.Row();
            _ok = Ui.Button(En ? "Got it" : "Понятно", Next, ButtonKind.Primary, 14f);
            _ok.style.marginRight = 8f;
            buttons.Add(_ok);
            buttons.Add(Ui.Button(En ? "Hide Tips" : "Скрыть подсказки", Hide, ButtonKind.Ghost, 13f));
            Panel.Add(buttons);
        }

        PlayerState MePlayer => _game.Players[Me];
        PlayerState Mentor => _game.Players[1];

        List<Step> BuildSteps() => new List<Step>
        {
            new Step
            {
                Title = "Добро пожаловать!",
                Text = () => "Это обучающая партия на маленькой карте — центр Москвы. Вы — дорожная компания, ваш соперник — Наставник. " +
                             "Цель — набрать больше очков: строить дороги и выполнять контракты мэрии. Я буду подсказывать по шагам."
            },
            new Step
            {
                Title = "Стартовые контракты",
                Text = () => "Внизу, в панели «Контракты», — контракты: соединить два района своими дорогами (подойдёт любое место района). " +
                             "Выполненный приносит очки, невыполненный в конце отнимает. Наведите на контракт — районы подсветятся. " +
                             "Оставьте хотя бы два и нажмите «Взять выбранные».",
                Done = () => _game.Phase != GamePhase.SetupContracts
            },
            new Step
            {
                Title = "Возьмите карты",
                Text = () => "За ход можно взять 2 карты — в панели «Действия» внизу: из открытого ряда или из колоды. " +
                             NeedText() + " Техника — джокер: заменяет любую карту, но открытую технику можно взять только одну за ход.",
                Done = () => _game.TurnNumber > 1 || MePlayer.Roads.Count > 0,
                HintRoad = true
            },
            new Step
            {
                Title = "Постройте дорогу",
                Text = () => "Голубым подсвечена дорога, которая ведёт к вашему контракту. Кликните по ней, когда хватит карт: " +
                             HintCostText() + " Если карт не хватает — снова возьмите карты. Доступные сейчас дороги мерцают.",
                Done = () => MePlayer.Roads.Count > 0,
                HintRoad = true
            },
            new Step
            {
                Title = "Очки за дороги",
                Text = () => $"Каждая дорога сразу приносит очки: длина 1 — {_game.Config.PointsForLength(1)}, 2 — {_game.Config.PointsForLength(2)}, " +
                             $"3 — {_game.Config.PointsForLength(3)}. Длинные дороги выгоднее. Участков дороги у вас ограничено: осталось {MePlayer.RoadPieces}."
            },
            new Step
            {
                Title = "Чужие дороги",
                When = () => Mentor.Roads.Count > 0,
                Text = () => $"Наставник тоже строит. Его дорога — не тупик: контракт засчитается, если на пути есть хотя бы одна ваша дорога " +
                             $"и не больше {_game.Config.MaxForeignRoadsPerContract} чужих. За каждую чужую вы платите владельцу {_game.Config.TollPerRoad} очка — " +
                             "сразу, когда контракт выполнен. И наоборот: по вашим дорогам платят вам."
            },
            new Step
            {
                Title = "Выполните контракт",
                Text = () => "Достройте путь между районами контракта. Подсвеченная дорога — следующая на этом пути. " + HintCostText(),
                Done = () => MePlayer.PaidRouteList.Count > 0,
                HintRoad = true
            },
            new Step
            {
                Title = "Контракт выполнен!",
                Text = () => "Очки за него посчитаются в конце, а влияние — валюта мэрии — уже у вас. Кнопка «Влияние» — в панели «Действия»: " +
                             "можно обновить рынок или взять контракты с выбором. Остаток влияния после партии уйдёт в профиль — на одежду персонажа и оформление."
            },
            new Step
            {
                Title = "Новые контракты",
                When = () => _game.Phase == GamePhase.AwaitingAction && MePlayer.Contracts.All(c => MePlayer.PaidRouteList.ContainsKey(c.Id)),
                Text = () => "Все ваши контракты выполнены. Пока участки ещё есть, возьмите новые — кнопка «Взять контракты» в панели «Действия» (это занимает ход). " +
                             "Оставить нужно хотя бы один. Или просто стройте длинные дороги — они тоже приносят очки.",
                Done = () => MePlayer.Contracts.Any(c => !MePlayer.PaidRouteList.ContainsKey(c.Id)) || _game.IsFinalRound
            },
            new Step
            {
                Title = "Скоро финал",
                When = () => _game.FinalWarned || _game.IsFinalRound,
                Text = () => $"Когда у кого-то остаётся {_game.Config.FinalRoundPieces} участка или меньше, начинается последний круг: у каждого ещё по ходу. " +
                             "Проверьте контракты: невыполненный в конце отнимает свои очки."
            },
            new Step
            {
                Title = "Последний круг",
                When = () => _game.IsFinalRound,
                Text = () => (_game.FinalOrderRoad != null
                                 ? $"Мэрия выставила финальный заказ — дорога в золотой рамке: кто построит её сейчас, получит +{_game.Config.FinalOrderBonus}. "
                                 : "") + "Это ваш последний ход: достройте контракт, если можно, или возьмите самую длинную доступную дорогу."
            }
        };

        // ================================================================== Подсказка дороги

        /// <summary>Следующая дорога на пути к невыполненному контракту — по тому же планировщику, что у ботов.</summary>
        Road HintedRoad()
        {
            var open = MePlayer.Contracts.Where(c => !MePlayer.PaidRouteList.ContainsKey(c.Id)).ToList();
            if (open.Count == 0 && _game.Phase == GamePhase.SetupContracts) open = MePlayer.PendingContracts.ToList();
            if (open.Count == 0) return null;
            var plan = new BotPlanner(_game, Me, 1.5f).BuildPlan(open);
            return plan.RoadsToBuild
                .Where(r => !_game.GetRoadOwner(r.Id).HasValue && r.Length <= MePlayer.RoadPieces && !_game.IsUnderRepair(r.Id))
                .OrderByDescending(r => _game.CurrentPlayerIndex == Me && _game.GetPaymentOptions(r.Id).Count > 0)
                .ThenBy(r => _game.BuildCost(r) - (r.Resource.HasValue ? MePlayer.CardCount(r.Resource.Value) : MaxBuilding()))
                .ThenBy(r => r.Id, System.StringComparer.Ordinal)
                .FirstOrDefault();
        }

        int MaxBuilding() => ResourceInfo.Building.Max(r => MePlayer.CardCount(r));

        string HintCostText()
        {
            var road = HintedRoad();
            if (road == null) return "";
            int cost = _game.BuildCost(road, MePlayer);
            string resource = road.Resource.HasValue ? $"«{ResourceInfo.RussianName(road.Resource.Value)}»" : "любого одного ресурса";
            int have = road.Resource.HasValue ? MePlayer.CardCount(road.Resource.Value) : MaxBuilding();
            have += MePlayer.CardCount(Resource.Machinery);
            return $"нужно {cost} карт(ы) {resource}, у вас подходящих {Mathf.Min(have, cost)}.";
        }

        string NeedText()
        {
            var road = HintedRoad();
            if (road == null) return "Берите карты тех ресурсов, из которых построены дороги к вашим контрактам.";
            string resource = road.Resource.HasValue ? $"«{ResourceInfo.RussianName(road.Resource.Value)}»" : "любого одного ресурса";
            return $"Для подсвеченной голубым дороги нужно {_game.BuildCost(road, MePlayer)} карт(ы) {resource}.";
        }

        // ================================================================== Показ

        void Next()
        {
            if (_index >= 0) _finished.Add(_index);
            Update();
        }

        void Hide()
        {
            _hidden = true;
            Update();
        }

        /// <summary>Обновить панель и подсветку — вызывается при каждом обновлении экрана партии.</summary>
        public void Update()
        {
            // Выполненные задания — пройдены. Показываем первый непройденный шаг, которому пора;
            // объяснение, которому ещё не пора (например, «Чужие дороги» до первой дороги Наставника), не мешает следующим.
            for (int i = 0; i < _steps.Count; i++)
                if (_steps[i].Done != null && _steps[i].Done()) _finished.Add(i);
            _index = -1;
            for (int i = 0; i < _steps.Count && _index < 0; i++)
                if (!_finished.Contains(i) && (_steps[i].When == null || _steps[i].When())) _index = i;
            bool over = _game.Phase == GamePhase.GameOver;
            var step = _index >= 0 ? _steps[_index] : null;
            bool visible = !_hidden && !over && step != null;
            Ui.Show(Panel, visible);
            _map.HintRoad = visible && step.HintRoad && _game.CurrentPlayerIndex == Me ? HintedRoad()?.Id : null;
            if (!visible) return;

            _counter.text = En ? $"Tutorial · step {_finished.Count + 1} of {_steps.Count}" : $"Обучение · шаг {_finished.Count + 1} из {_steps.Count}";
            _title.text = En ? EnglishStepTitles[_index] : step.Title;
            _text.text = En ? EnglishStepText(_index) : step.Text();
            Ui.Show(_ok, step.Done == null);
            _ok.text = _index == 0 ? (En ? "Let’s Start" : "Начнём") : (En ? "Got it" : "Понятно");

            // В левом верхнем углу карты.
            var bounds = _map.worldBound;
            if (Panel.parent != null && !float.IsNaN(bounds.width) && bounds.width > 0f)
            {
                var topLeft = Panel.parent.WorldToLocal(bounds.position);
                Panel.style.left = topLeft.x + 14f;
                Panel.style.top = topLeft.y + 14f;
            }
        }
    }
}
