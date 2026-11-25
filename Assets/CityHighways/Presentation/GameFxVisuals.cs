using System;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Temporary visual objects and their animation geometry.
    /// GameView selects safe destinations, card visibility and sound/completion actions.</summary>
    public sealed class GameFxVisuals
    {
        readonly GameFxLifetime _effects;

        public GameFxVisuals(GameFxLifetime effects) => _effects = effects;

        public void FlyCard(Vector2 start, Vector2 end, float width, float height, Resource resource,
            bool reveal, float delay, Action playSound, Action finished)
        {
            var card = new CardView(width, height, false);
            card.pickingMode = PickingMode.Ignore;
            if (reveal) card.Set(resource);
            else card.ShowBack();
            card.style.position = Position.Absolute;
            card.style.left = start.x;
            card.style.top = start.y;
            card.style.opacity = 0f;
            _effects.Attach(card);

            _effects.After(delay, playSound);
            bool reducedMotion = Ui.ReducedMotion;
            _effects.Add(GameFxTuning.CardFlightDuration(reducedMotion), k =>
            {
                float eased = Ui.EaseInOutCubic(k);
                var position = Vector2.Lerp(start, end, eased);
                position.y -= Mathf.Sin(k * Mathf.PI) * GameFxTuning.CardArcHeight(reducedMotion);
                card.style.left = position.x;
                card.style.top = position.y;
                card.style.opacity = Mathf.Clamp01(k * 8f) * (k > 0.85f ? (1f - k) / 0.15f : 1f);
            }, () =>
            {
                _effects.Remove(card);
                finished?.Invoke();
            }, delay);
        }

        public void FlyCoin(Vector2 from, Vector2 to, int toll, float delay, Action playSound, Action finished)
        {
            const float size = 30f;
            var coin = Ui.Box();
            coin.pickingMode = PickingMode.Ignore;
            coin.style.position = Position.Absolute;
            coin.style.width = size;
            coin.style.height = size;
            coin.style.backgroundColor = new Color(1f, 0.80f, 0.25f);
            Ui.Radius(coin, size / 2f);
            Ui.Border(coin, 2f, new Color(0.72f, 0.50f, 0.08f));
            coin.style.justifyContent = Justify.Center;
            coin.style.alignItems = Align.Center;
            var label = Ui.Text($"+{toll}", 12f, new Color(0.30f, 0.20f, 0.02f), true);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            coin.Add(label);
            coin.style.opacity = 0f;
            _effects.Attach(coin);

            _effects.After(delay, playSound);
            _effects.Add(1.0f, k =>
            {
                // Сначала монетка выпрыгивает над дорогой, потом по дуге летит к владельцу.
                float pop = Mathf.Clamp01(k / 0.25f);
                float fly = Ui.EaseInOutCubic(Mathf.Clamp01((k - 0.25f) / 0.75f));
                var position = Vector2.Lerp(from + new Vector2(0f, -26f * Ui.EaseOutCubic(pop)), to, fly);
                position.y -= Mathf.Sin(fly * Mathf.PI) * 60f;
                coin.style.left = position.x - size / 2f;
                coin.style.top = position.y - size / 2f;
                float scale = GameFxTuning.CoinScale(k);
                coin.style.scale = new Scale(new Vector3(scale, scale, 1f));
                coin.style.opacity = k > 0.9f ? (1f - k) / 0.1f : 1f;
            }, () =>
            {
                _effects.Remove(coin);
                finished?.Invoke();
            }, delay, false);
        }

        public void FloatAt(Vector2 at, string text, Color color)
        {
            var label = Ui.Text(text, 20f, color, true);
            label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.8f);
            label.style.unityTextOutlineWidth = 0.25f;
            label.style.left = at.x - 14f;
            label.style.top = at.y - 12f;
            _effects.Attach(label);
            _effects.Add(1.3f, k =>
            {
                label.style.top = at.y - 12f - 34f * Ui.EaseOutCubic(k);
                label.style.opacity = k < 0.1f ? k / 0.1f : k > 0.7f ? (1f - k) / 0.3f : 1f;
            }, () => _effects.Remove(label), 0f, false);
        }
    }
}
