using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Минимальная система анимаций: функция получает прогресс 0..1 каждый кадр.</summary>
    public sealed class Tweener
    {
        sealed class Tween
        {
            public float Duration;
            public float Delay;
            public float Elapsed;
            public bool Blocking;
            public Action<float> Update;
            public Action Done;
            public object Scope;
            public bool Cancelled;
        }

        readonly List<Tween> _tweens = new List<Tween>();

        /// <summary>Сколько идёт «важных» анимаций — боты ждут, пока они закончатся.</summary>
        public int BlockingCount
        {
            get
            {
                int count = 0;
                foreach (var t in _tweens)
                    if (t.Blocking) count++;
                return count;
            }
        }

        public void Add(float duration, Action<float> update, Action done = null, float delay = 0f, bool blocking = true, object scope = null)
        {
            _tweens.Add(new Tween
            {
                Duration = duration,
                Delay = delay,
                Update = update,
                Done = done,
                Blocking = blocking,
                Scope = scope
            });
        }

        /// <summary>Выполнить действие через delay секунд.</summary>
        public void After(float delay, Action action, bool blocking = false, object scope = null) =>
            Add(0f, null, action, delay, blocking, scope);

        public void Tick(float deltaTime)
        {
            if (_tweens.Count == 0) return;

            // Копия: в Done можно запускать новые анимации.
            var current = _tweens.ToArray();
            foreach (var tween in current)
            {
                if (tween.Cancelled) continue;
                tween.Elapsed += deltaTime;
                if (tween.Elapsed < tween.Delay) continue;

                float progress = tween.Duration <= 0f
                    ? 1f
                    : Mathf.Clamp01((tween.Elapsed - tween.Delay) / tween.Duration);

                try
                {
                    tween.Update?.Invoke(progress);
                    if (!tween.Cancelled && progress >= 1f)
                    {
                        _tweens.Remove(tween);
                        tween.Done?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    _tweens.Remove(tween);
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>Cancel only one owner's pending work, without calling completion callbacks.</summary>
        public void CancelScope(object scope)
        {
            if (scope == null) return;
            for (int i = _tweens.Count - 1; i >= 0; i--)
            {
                var tween = _tweens[i];
                if (!ReferenceEquals(tween.Scope, scope)) continue;
                tween.Cancelled = true;
                _tweens.RemoveAt(i);
            }
        }

        public void Clear()
        {
            // A callback may clear the queue while Tick is iterating its snapshot.
            foreach (var tween in _tweens) tween.Cancelled = true;
            _tweens.Clear();
        }
    }
}
