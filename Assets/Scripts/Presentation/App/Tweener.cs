using System;
using System.Collections.Generic;
using UnityEngine;

namespace RailGame.Presentation.App
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

        public void Add(float duration, Action<float> update, Action done = null, float delay = 0f, bool blocking = true)
        {
            _tweens.Add(new Tween
            {
                Duration = duration,
                Delay = delay,
                Update = update,
                Done = done,
                Blocking = blocking
            });
        }

        /// <summary>Выполнить действие через delay секунд.</summary>
        public void After(float delay, Action action, bool blocking = false) =>
            Add(0f, null, action, delay, blocking);

        public void Tick(float deltaTime)
        {
            if (_tweens.Count == 0) return;

            // Копия: в Done можно запускать новые анимации.
            var current = _tweens.ToArray();
            foreach (var tween in current)
            {
                tween.Elapsed += deltaTime;
                if (tween.Elapsed < tween.Delay) continue;

                float progress = tween.Duration <= 0f
                    ? 1f
                    : Mathf.Clamp01((tween.Elapsed - tween.Delay) / tween.Duration);

                try
                {
                    tween.Update?.Invoke(progress);
                    if (progress >= 1f)
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

        public void Clear() => _tweens.Clear();
    }
}
