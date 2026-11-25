using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Classic notification UI and transient localized error state.
    /// GameView decides when to notify; this component owns only presentation.</summary>
    public sealed class GameFxNotifications
    {
        readonly GameFxLifetime _effects;
        readonly VisualElement _eventCard, _eventPill, _toastHolder;
        readonly Label _toast;
        readonly Func<float> _screenWidth;
        int _toastVersion, _eventGlowVersion;
        string _flash, _flashPrefixKey;
        float _flashUntil;

        public GameFxNotifications(GameFxLifetime effects, VisualElement layer,
            VisualElement eventCard, VisualElement eventPill, Func<float> screenWidth, float topBarHeight)
        {
            _effects = effects;
            _eventCard = eventCard;
            _eventPill = eventPill;
            _screenWidth = screenWidth;
            _toastHolder = Ui.Row();
            _toastHolder.style.position = Position.Absolute;
            _toastHolder.style.left = 0f;
            _toastHolder.style.right = 0f;
            _toastHolder.pickingMode = PickingMode.Ignore;
            _toastHolder.style.top = topBarHeight + 120f;
            _toastHolder.style.justifyContent = Justify.Center;
            _toast = Ui.Text("", 20f, Theme.Gold, true);
            _toast.pickingMode = PickingMode.Ignore;
            _toast.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.92f);
            _toast.style.whiteSpace = WhiteSpace.Normal;
            _toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            Ui.Radius(_toast, 22f);
            Ui.Padding(_toast, 10f, 24f);
            _toastHolder.Add(_toast);
            Ui.Show(_toastHolder, false);
            layer.Add(_toastHolder);
        }

        public bool HasFlash => _flash != null;
        public string FlashText => _flash == null ? "" :
            (_flashPrefixKey == null ? "" : LocalizationService.Get(_flashPrefixKey)) +
            LocalizationService.TranslateGameError(_flash);

        public void Flash(string message, string prefixKey, float now)
        {
            _flash = message;
            _flashPrefixKey = prefixKey;
            _flashUntil = now + 3.5f;
        }

        public bool ExpireFlash(float now)
        {
            if (!HasFlash || now <= _flashUntil) return false;
            _flash = null;
            _flashPrefixKey = null;
            return true;
        }

        public void AdaptWidth(float width) => _toast.style.maxWidth = GameFxTuning.ToastMaxWidth(width);

        public void Toast(string text, Color color)
        {
            _toast.text = text;
            _toast.style.color = color;
            int version = ++_toastVersion;
            bool reducedMotion = Ui.ReducedMotion;
            _toastHolder.style.opacity = 0f;
            _toastHolder.style.top = GameFxTuning.ToastTopAt(0f, reducedMotion)
                + GameFxTuning.ToastVerticalOffset(_screenWidth());
            Ui.Show(_toastHolder, true);

            _effects.Add(2.4f, k =>
            {
                if (version != _toastVersion) return;
                _toastHolder.style.opacity = GameFxTuning.ToastOpacity(k);
                _toastHolder.style.top = GameFxTuning.ToastTopAt(k, reducedMotion)
                    + GameFxTuning.ToastVerticalOffset(_screenWidth());
            }, () =>
            {
                if (version == _toastVersion) Ui.Show(_toastHolder, false);
            }, 0f, false);
        }

        public void GlowEventCard(Color color)
        {
            int version = ++_eventGlowVersion;
            var start = new Color(color.r, color.g, color.b, 1f);
            _effects.Add(2.0f, k =>
            {
                if (version != _eventGlowVersion) return;
                float s = GameFxTuning.EventGlowStrength(k, Ui.ReducedMotion);
                var glow = Color.Lerp(Theme.PanelLight, Color.Lerp(Theme.PanelLight, start, 0.28f), s);
                _eventCard.style.backgroundColor = glow;
                _eventPill.style.backgroundColor = Color.Lerp(Theme.PanelLight, Color.Lerp(Theme.PanelLight, start, 0.45f), s);
            }, () =>
            {
                if (version != _eventGlowVersion) return;
                _eventCard.style.backgroundColor = Theme.PanelLight;
                _eventPill.style.backgroundColor = Theme.PanelLight;
            }, 0f, false);
        }

        public void Reset()
        {
            ++_toastVersion;
            ++_eventGlowVersion;
            _flash = null;
            _flashPrefixKey = null;
            Ui.Show(_toastHolder, false);
            _eventCard.style.backgroundColor = Theme.PanelLight;
            _eventPill.style.backgroundColor = Theme.PanelLight;
        }
    }
}
