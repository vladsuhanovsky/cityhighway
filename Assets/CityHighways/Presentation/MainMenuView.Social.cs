using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// «Новая игра»: баннер призрака над кнопкой «Начать игру» — лучший результат друга (или другого профиля этого
    /// компьютера, или свой) на выбранной карте в выбранном режиме, с кнопками «Бросить вызов» (рейд) и «▶ Повтор».
    /// </summary>
    public sealed partial class MainMenuView
    {
        VisualElement _ghostBanner;
        Label _ghostText, _ghostOthers;
        Button _ghostRaid, _ghostWatch;
        Ghost _bannerGhost;

        VisualElement BuildGhostBanner()
        {
            _ghostBanner = Ui.Row();
            _ghostBanner.style.alignItems = Align.Center;
            _ghostBanner.style.marginTop = 16f;
            _ghostBanner.style.backgroundColor = new Color(0.24f, 0.30f, 0.50f, 0.85f);
            Ui.Radius(_ghostBanner, 12f);
            Ui.Padding(_ghostBanner, 12f, 16f);
            _ghostBanner.style.borderLeftWidth = 5f;
            _ghostBanner.style.borderLeftColor = new Color(0.62f, 0.74f, 1f);

            var icon = new MenuIcon(MenuIcon.Glyph.Person, 30f, new Color(0.78f, 0.86f, 1f));
            icon.style.marginRight = 14f;
            _ghostBanner.Add(icon);
            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            texts.style.flexShrink = 1f;
            _ghostText = Ui.Text("", 16f, Theme.Text, true);
            _ghostText.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(_ghostText);
            _ghostOthers = Ui.Text("", 12f, new Color(0.78f, 0.84f, 0.95f));
            _ghostOthers.style.whiteSpace = WhiteSpace.Normal;
            _ghostOthers.style.marginTop = 2f;
            texts.Add(_ghostOthers);
            _ghostBanner.Add(texts);

            _ghostWatch = Ui.Button("▶ Повтор", () =>
            {
                var ghost = _bannerGhost;
                ghost?.LoadReplay(replay => Replays.Play?.Invoke(replay), error => _ghostOthers.text = error);
            }, ButtonKind.Secondary, 14f);
            _ghostWatch.style.marginLeft = 10f;
            _ghostBanner.Add(_ghostWatch);
            _ghostRaid = Ui.Button("Бросить вызов", () =>
            {
                var ghost = _bannerGhost;
                if (ghost == null) return;
                _ghostOthers.text = "Готовим рейд…";
                ghost.LoadReplay(_ => _app.StartRaid(ghost), error => _ghostOthers.text = error);
            }, ButtonKind.Primary, 14f);
            _ghostRaid.style.marginLeft = 8f;
            _ghostRaid.tooltip = "Рейд: те же карта, колода, соперники (боты) и компании, что в партии рекордсмена. Вы садитесь на его место — дальше решают ваши ходы.";
            _ghostBanner.Add(_ghostRaid);

            // Рекорды друзей пришли из облака — обновить баннер, пока меню на экране.
            Action onChanged = null;
            onChanged = () =>
            {
                if (Root.panel == null)
                {
                    SocialCloud.Changed -= onChanged;
                    return;
                }
                RefreshGhostBanner();
            };
            SocialCloud.Changed += onChanged;

            Ui.Show(_ghostBanner, false);
            return _ghostBanner;
        }

        void RefreshGhostBanner()
        {
            if (_ghostBanner == null) return;
            if (_setup.magnate)
            {
                Ui.Show(_ghostBanner, false);
                return;
            }
            string mode = _setup.cooperative ? "coop" : "classic";
            var ghosts = Ghosts.For(mode, _setup.mapResourcePath);
            _bannerGhost = ghosts.FirstOrDefault();
            Ui.Show(_ghostBanner, _bannerGhost != null);
            if (_bannerGhost == null) return;
            _ghostText.text = _bannerGhost.Challenge;
            var others = ghosts.Skip(1).Where(g => g.Source != "self").Take(4).Select(g => $"{g.Name} {g.Score}").ToList();
            var own = ghosts.FirstOrDefault(g => g.Source == "self");
            string line = others.Count > 0 ? "Ещё рекорды: " + string.Join(", ", others) + "." : "";
            if (own != null && own != _bannerGhost) line += (line.Length > 0 ? " " : "") + $"Ваш рекорд: {own.Score}.";
            if (line.Length == 0) line = "Рейд — та же раздача и те же соперники, что у рекордсмена.";
            _ghostOthers.text = line;
            _ghostRaid.text = _bannerGhost.Source == "self" ? "Побить свой рекорд" : "Бросить вызов";
        }
    }
}
