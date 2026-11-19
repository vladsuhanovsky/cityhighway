using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Обучение магната: карточка-наставник в правом нижнем углу карты ведёт по первому раунду.
    /// Шаги с действием (купить дорогу, выбрать фуру, взять груз, «Готово») переходят дальше сами, как только
    /// игрок это сделал; нужная кнопка или фура в нижней панели подсвечивается золотой рамкой, хабы на карте пульсируют.
    /// Пройденное или пропущенное обучение больше не показывается само (GameSetup.magnateTutorialDone);
    /// открыть заново — «Меню» → «? Обучение» в правом верхнем углу.
    /// </summary>
    public sealed partial class MagnateView
    {
        const int CoachIntro = 0, CoachHubs = 1, CoachBuild = 2, CoachNext = 3, CoachTruck = 4, CoachCargo = 5,
            CoachDone = 6, CoachWatch = 7, CoachEnd = 8;

        int _coachStep = -1;
        string _coachTarget;
        int _coachRoads;
        VisualElement _coach;
        Label _coachTitle, _coachText, _coachCounter;
        VisualElement _coachButtons;

        static readonly (string title, string text, string target, bool action)[] CoachSteps =
        {
            ("Добро пожаловать в «Логистический магнат»",
                "Вы владеете транспортной компанией. Ваши фуры забирают грузы на хабах и везут получателям — за каждую доставку дают очки. " +
                "Побеждает компания, набравшая больше всех очков к концу последнего раунда.", null, false),
            ("Хабы — источники грузов",
                "Аэропорты, вокзалы и порты пульсируют на карте. Квадратики над хабом — грузы, цвет показывает вид. " +
                "Красная рамка — скоропорт, который вот-вот испортится, золотая точка — груз для заказа мэрии.", null, false),
            ("Стройка: купите дорогу",
                "Раунд начинается со стройки. Асфальтовые дороги — городские, по ним ездят все. Пунктир — дорога, которую можно построить: кликните пунктир рядом с вашими фурами, " +
                "чтобы купить её: по своим дорогам вы ездите бесплатно, а соперники платят вам за проезд.", null, true),
            ("Переходите к маршрутам",
                "Когда очки стройки потрачены, нажмите «Дальше: маршруты фур» в нижней панели — кнопка подсвечена.", "next", true),
            ("Выберите фуру",
                "Кликните фуру в разделе «Маршруты фур» внизу — откроется её карточка. На карте фура — кузов цвета вашей компании.", "truck", true),
            ("Возьмите груз",
                "Если фура стоит на хабе, нажмите «Взять» у груза в её карточке. Если рядом грузов нет, откройте «Грузы» внизу (список появится справа на карте) и нажмите «Выбрать» — " +
                "ближайшая свободная фура возьмёт груз или поедет за ним. Клик по строке груза — подробности и путь на карте.", "cargo", true),
            ("Отправляйте фуры",
                "Спланируйте остальные фуры или сразу нажмите «Готово». Все компании едут одновременно. Если на дорогу заедет больше фур, " +
                "чем она пропускает, будет пробка, и все они простоят раунд.", "done", true),
            ("Фуры в пути",
                "Скоропорт едет быстрее, тяжёлые грузы — медленнее. Над очередью в пробке крутятся часы, перекрытая дорога — со шлагбаумом.", null, true),
            ("Дальше — сами",
                "Разделы «Игроки», «Грузы» и «Заказы мэрии» внизу показывают счётчики и по клику открываются панелью справа на карте, справа вверху — «Журнал», «Меню» (правила и это обучение) и «Настройки». Справа вверху на карте — " +
                "«Слои карты»: можно спрятать подписи, грузы, свободные дороги или включить загрузку дорог.", null, false),
        };

        void BuildCoach()
        {
            _coach = new VisualElement();
            _coach.style.position = Position.Absolute;
            _coach.style.right = 14f;
            _coach.style.bottom = 14f;
            _coach.style.width = 420f;
            _coach.style.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 0.96f);
            Ui.Padding(_coach, 12f, 14f);
            Ui.Radius(_coach, 12f);
            Ui.Border(_coach, 1.5f, Theme.Gold);
            Ui.Shadow(_coach, 12f);
            var head = Ui.Row();
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.alignItems = Align.Center;
            _coachTitle = Ui.Text("", 15f, Theme.Gold, true);
            _coachTitle.style.whiteSpace = WhiteSpace.Normal;
            _coachTitle.style.flexShrink = 1f;
            head.Add(_coachTitle);
            _coachCounter = Ui.Text("", 11f, Theme.TextDim);
            _coachCounter.style.marginLeft = 8f;
            head.Add(_coachCounter);
            _coach.Add(head);
            _coachText = Ui.Text("", 13f, Theme.Text);
            _coachText.style.whiteSpace = WhiteSpace.Normal;
            _coachText.style.marginTop = 6f;
            _coach.Add(_coachText);
            _coachButtons = Ui.Row();
            _coachButtons.style.marginTop = 10f;
            _coach.Add(_coachButtons);
            Ui.Show(_coach, false);
            _mapArea.Add(_coach);
        }

        void StartCoach() => SetCoachStep(CoachIntro);

        void SetCoachStep(int step)
        {
            if (step >= CoachSteps.Length) step = -1;
            _coachStep = step;
            if (step < 0)
            {
                _coachTarget = null;
                Ui.Show(_coach, false);
                _dirty = true;
                return;
            }
            var info = CoachSteps[step];
            _coachTarget = info.target;
            if (step == CoachBuild) _coachRoads = OwnRoads();
            _coachTitle.text = info.title;
            _coachText.text = info.text;
            _coachCounter.text = $"{step + 1}/{CoachSteps.Length}";
            _coachButtons.Clear();
            if (!info.action || step == CoachBuild)
            {
                var next = Ui.Button(step == CoachEnd ? "Понятно" : info.action ? "Пропустить шаг" : "Дальше",
                    () => { _app.Sound.Play(Sfx.Click, 0.5f); if (step == CoachEnd) FinishCoach(); else SetCoachStep(step + 1); },
                    info.action ? ButtonKind.Secondary : ButtonKind.Primary, 13f);
                next.style.marginRight = 8f;
                _coachButtons.Add(next);
            }
            if (step != CoachEnd)
                _coachButtons.Add(Ui.Button("Пропустить обучение", FinishCoach, ButtonKind.Ghost, 12f));
            _dirty = true;
            UpdateCoachVisibility();
        }

        /// <summary>
        /// Сохранить настройки магната (обучение пройдено, слои). У тренировочной партии свой GameSetup — тогда
        /// переносим только эти два поля в сохранённые настройки игрока, ничего больше не трогая.
        /// </summary>
        void SaveSetup()
        {
            if (!_setup.practice)
            {
                _setup.Save();
                return;
            }
            var stored = GameSetup.Load();
            stored.magnateTutorialDone = _setup.magnateTutorialDone;
            stored.magnateLayers = _setup.magnateLayers;
            stored.Save();
        }

        void FinishCoach()
        {
            _setup.magnateTutorialDone = true;
            SaveSetup();
            SetCoachStep(-1);
        }

        int OwnRoads() => _game.Map.Roads.Count(r => _game.Board.GetRoadOwner(r.Id) == _game.CurrentPlayerIndex);

        void UpdateCoachVisibility()
        {
            if (_coachStep < 0) return;
            bool always = _coachStep == CoachIntro || _coachStep == CoachHubs || _coachStep == CoachWatch || _coachStep == CoachEnd;
            Ui.Show(_coach, !_resultsShown && (always || HumanTurn));
        }

        /// <summary>Шаги с действием идут дальше сами, когда игрок сделал то, о чём просила подсказка.</summary>
        void CoachTick()
        {
            if (_coachStep < 0) return;
            UpdateCoachVisibility();
            switch (_coachStep)
            {
                case CoachBuild:
                    if (_game.Phase == MagnatePhase.Plan && HumanTurn) SetCoachStep(CoachTruck);
                    else if (HumanTurn && OwnRoads() > _coachRoads) SetCoachStep(CoachNext);
                    break;
                case CoachNext:
                    if (_game.Phase == MagnatePhase.Plan && HumanTurn) SetCoachStep(CoachTruck);
                    break;
                case CoachTruck:
                    if (HumanTurn && _game.Phase == MagnatePhase.Plan && _selectedTruck >= 0) SetCoachStep(CoachCargo);
                    break;
                case CoachCargo:
                    if (HumanTurn && _game.Trucks.Any(t => t.Owner == _game.CurrentPlayerIndex && (t.Route.Count > 0 || t.PlannedCargo != null)))
                        SetCoachStep(CoachDone);
                    break;
                case CoachDone:
                    if (_animating != null) SetCoachStep(CoachWatch);
                    break;
                case CoachWatch:
                    if (_animating == null && _shownReport != null) SetCoachStep(CoachEnd);
                    break;
            }
        }
    }
}
