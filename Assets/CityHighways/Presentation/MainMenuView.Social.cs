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
        readonly GhostBannerRequest _ghostRequest = new GhostBannerRequest();

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

            _ghostWatch = Ui.Button("▶ Повтор", () => RequestGhost(false), ButtonKind.Secondary, 14f);
            _ghostWatch.style.marginLeft = 10f;
            _ghostBanner.Add(_ghostWatch);
            _ghostRaid = Ui.Button("Бросить вызов", () => RequestGhost(true), ButtonKind.Primary, 14f);
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

        void CancelGhostRequest()
        {
            _ghostRequest.Cancel();
            _bannerGhost = null;
        }

        void RequestGhost(bool raid)
        {
            if (Root.panel == null || _page != Page.NewGame || _bannerGhost == null) return;
            var ghost = _bannerGhost;
            if (!_ghostRequest.TryBegin(out int token)) return;
            _ghostWatch.SetEnabled(false);
            _ghostRaid.SetEnabled(false);
            bool english = LocalizationService.Language == "en";
            _ghostOthers.text = raid ? (english ? "Preparing raid…" : "Готовим рейд…")
                : (english ? "Loading replay…" : "Загружаем повтор…");

            bool Complete()
            {
                if (Root.panel == null || _page != Page.NewGame ||
                    !_ghostRequest.TryComplete(token)) return false;
                _ghostWatch.SetEnabled(true);
                _ghostRaid.SetEnabled(true);
                return true;
            }

            ghost.LoadReplay(replay =>
            {
                if (!Complete()) return;
                if (raid) _app.StartRaid(ghost);
                else Replays.Play?.Invoke(replay);
            }, error =>
            {
                if (Complete()) _ghostOthers.text = error;
            });
        }

        void RefreshGhostBanner()
        {
            if (_ghostBanner == null) return;
            if (_setup.magnate)
            {
                CancelGhostRequest();
                Ui.Show(_ghostBanner, false);
                return;
            }
            string mode = _setup.cooperative ? "coop" : "classic";
            var ghosts = Ghosts.For(mode, _setup.mapResourcePath, _setup.coopDifficulty);
            var next = ghosts.FirstOrDefault();
            if (!GhostBannerRequest.SameRecord(_bannerGhost, next))
            {
                CancelGhostRequest();
                _bannerGhost = next;
            }
            _ghostRaid.SetEnabled(!_ghostRequest.Pending);
            _ghostWatch.SetEnabled(!_ghostRequest.Pending);
            Ui.Show(_ghostBanner, _bannerGhost != null);
            if (_bannerGhost == null) return;
            _ghostText.text = _bannerGhost.Challenge;
            if (_ghostRequest.Pending) return;
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
