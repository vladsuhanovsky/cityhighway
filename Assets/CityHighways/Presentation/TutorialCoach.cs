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
            _ok = Ui.Button("Понятно", Next, ButtonKind.Primary, 14f);
            _ok.style.marginRight = 8f;
            buttons.Add(_ok);
            buttons.Add(Ui.Button("Скрыть подсказки", Hide, ButtonKind.Ghost, 13f));
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

            _counter.text = $"Обучение · шаг {_finished.Count + 1} из {_steps.Count}";
            _title.text = step.Title;
            _text.text = step.Text();
            Ui.Show(_ok, step.Done == null);
            _ok.text = _index == 0 ? "Начнём" : "Понятно";

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
