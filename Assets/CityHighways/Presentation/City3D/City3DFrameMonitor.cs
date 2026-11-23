using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Lightweight frame-interval sampling for the active generated 3D scene.
    /// Captures wall-clock frame intervals, NOT isolated CPU/GPU rendering times.
    /// </summary>
    public sealed class City3DFrameMonitor : MonoBehaviour
    {
        const int Capacity = 600;
        const float WarmupSeconds = 2f;
        readonly float[] frameMs = new float[Capacity];
        int next, count;
        float warmupUntil;

        [Serializable]
        public struct Snapshot
        {
            public int Frames;
            public float AverageMs;
            public float P95Ms;
            public float P99Ms;
            public float MaxMs;
            public float EstimatedFps;
            public float OnePercentLowFps;
        }

        void OnEnable()
        {
            ResetSamples();
            warmupUntil = Time.realtimeSinceStartup + WarmupSeconds;
        }

        void Update()
        {
            if (Time.realtimeSinceStartup < warmupUntil) return;
            RecordFrame(Time.unscaledDeltaTime * 1000f);
        }

        public void ResetSamples()
        {
            next = 0;
            count = 0;
        }

        /// <summary>Pure ring-buffer insertion; public to allow deterministic Edit Mode tests.</summary>
        public void RecordFrame(float milliseconds)
        {
            if (float.IsNaN(milliseconds) || float.IsInfinity(milliseconds) ||
                milliseconds <= 0f) return;
            frameMs[next] = milliseconds;
            next = (next + 1) % Capacity;
            if (count < Capacity) count++;
        }

        /// <summary>Computes percentiles only when requested, never during Update.</summary>
        public Snapshot Capture()
        {
            var result = new Snapshot { Frames = count };
            if (count == 0) return result;
            var values = new float[count];
            Array.Copy(frameMs, values, count);
            Array.Sort(values);
            double sum = 0d;
            foreach (float value in values) sum += value;
            result.AverageMs = (float)(sum / count);
            result.P95Ms = Percentile(values, 0.95f);
            result.P99Ms = Percentile(values, 0.99f);
            result.MaxMs = values[count - 1];
            result.EstimatedFps = 1000f / result.AverageMs;
            result.OnePercentLowFps = 1000f / result.P99Ms;
            return result;
        }

        static float Percentile(float[] sorted, float percentile)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt(sorted.Length * percentile) - 1,
                0, sorted.Length - 1);
            return sorted[index];
        }
    }
}
