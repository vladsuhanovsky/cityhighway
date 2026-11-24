using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    public sealed partial class MagnateView
    {
        GameHudLayout.Tier? _lastMagnateHeaderTier;

        /// <summary>
        /// Hide optional mode branding, never action buttons. Round and
        /// event details stay available via their existing tooltips, while
        /// a flexing player strip yields space to top-bar actions.
        /// </summary>
        void AdaptMagnateHeader(float panelWidth)
        {
            if (float.IsNaN(panelWidth) || float.IsInfinity(panelWidth) ||
                panelWidth < 100f) return;

            // The guide also responds to resizing *within* the same tier.
            _gamepadGuide.style.maxWidth = GameHudLayout.GuideMaxWidth(panelWidth);

            var tier = GameHudLayout.ForWidth(panelWidth);
            if (_lastMagnateHeaderTier == tier) return;
            _lastMagnateHeaderTier = tier;
            Ui.Show(_magnateModeTitle, GameHudLayout.ShowMagnateTitle(tier));
            Ui.Padding(_magnateTopBar, 0f, GameHudLayout.HorizontalPadding(tier));
            _roundLabel.style.maxWidth = GameHudLayout.MagnateRoundMaxWidth(tier);
            _eventChip.style.maxWidth = GameHudLayout.MagnateEventMaxWidth(tier);
            _roundLabel.style.marginRight = GameHudLayout.ActionGap(tier);
            _eventChip.style.marginRight = GameHudLayout.ActionGap(tier);
            _journalButton.style.marginRight = GameHudLayout.ActionGap(tier);
        }
    }
}
