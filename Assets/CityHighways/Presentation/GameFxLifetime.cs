using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Owns pending gameplay effects and their temporary visual elements.
    /// Persistent HUD, tooltip and tutorial elements are not registered here.</summary>
    public sealed class GameFxLifetime
    {
        readonly Tweener _tweener;
        readonly VisualElement _layer;
        readonly HashSet<VisualElement> _temporary = new HashSet<VisualElement>();

        public GameFxLifetime(Tweener tweener, VisualElement layer)
        {
            _tweener = tweener;
            _layer = layer;
        }

        public void Add(float duration, Action<float> update, Action done = null, float delay = 0f, bool blocking = true) =>
            _tweener.Add(duration, update, done, delay, blocking, this);

        public void After(float delay, Action action, bool blocking = false) =>
            _tweener.After(delay, action, blocking, this);

        public void Attach(VisualElement element)
        {
            _layer.Add(element);
            _temporary.Add(element);
        }

        public void Remove(VisualElement element)
        {
            if (_temporary.Remove(element) && element.parent == _layer)
                element.RemoveFromHierarchy();
        }

        /// <summary>Cancel rather than finish: stale callbacks must not mutate the next game or play audio.</summary>
        public void Reset()
        {
            _tweener.CancelScope(this);
            foreach (var element in _temporary)
                if (element.parent == _layer) element.RemoveFromHierarchy();
            _temporary.Clear();
        }
    }
}
