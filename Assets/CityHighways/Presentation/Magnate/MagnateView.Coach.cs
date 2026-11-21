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
            (LocalizationService.Get("magnate.tutorial.001"),
                LocalizationService.Get("magnate.tutorial.002") +
                LocalizationService.Get("magnate.tutorial.003"), null, false),
            (LocalizationService.Get("magnate.tutorial.004"),
                LocalizationService.Get("magnate.tutorial.005") +
                LocalizationService.Get("magnate.tutorial.006"), null, false),
            (LocalizationService.Get("magnate.tutorial.007"),
                LocalizationService.Get("magnate.tutorial.008") +
                LocalizationService.Get("magnate.tutorial.009"), null, true),
            (LocalizationService.Get("magnate.tutorial.010"),
                LocalizationService.Get("magnate.tutorial.011"), "next", true),
            (LocalizationService.Get("magnate.tutorial.012"),
                LocalizationService.Get("magnate.tutorial.013"), "truck", true),
            (LocalizationService.Get("magnate.tutorial.014"),
                LocalizationService.Get("magnate.tutorial.015") +
                LocalizationService.Get("magnate.tutorial.016"), "cargo", true),
            (LocalizationService.Get("magnate.tutorial.017"),
                LocalizationService.Get("magnate.tutorial.018") +
                LocalizationService.Get("magnate.tutorial.019"), "done", true),
            (LocalizationService.Get("magnate.tutorial.020"),
                LocalizationService.Get("magnate.tutorial.021"), null, true),
            (LocalizationService.Get("magnate.tutorial.022"),
                LocalizationService.Get("magnate.tutorial.023") +
                LocalizationService.Get("magnate.tutorial.024"), null, false),
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
                var next = Ui.Button(En ? (step == CoachEnd ? "Got it" : info.action ? "Skip Step" : "Next") : (step == CoachEnd ? "Понятно" : info.action ? "Пропустить шаг" : "Дальше"),
                    () => { _app.Sound.Play(Sfx.Click, 0.5f); if (step == CoachEnd) FinishCoach(); else SetCoachStep(step + 1); },
                    info.action ? ButtonKind.Secondary : ButtonKind.Primary, 13f);
                next.style.marginRight = 8f;
                _coachButtons.Add(next);
            }
            if (step != CoachEnd)
                _coachButtons.Add(Ui.Button(En ? "Skip Tutorial" : "Пропустить обучение", FinishCoach, ButtonKind.Ghost, 12f));
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
