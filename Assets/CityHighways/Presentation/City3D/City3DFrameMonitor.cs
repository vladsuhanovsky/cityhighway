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
        public const int MinimumFrames = 60;
        public const float WarmupSeconds = 2f;
        readonly float[] frameMs = new float[Capacity];
        int next, count;
        float warmupUntil;

        public int SampleCount => count;
        public int SampleCapacity => Capacity;
        public bool IsWarmingUp => Time.realtimeSinceStartup < warmupUntil;
        public bool HasMinimumSamples => count >= MinimumFrames;

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

        /// <summary>
        /// Advisory heuristics for unstable sampling, independent from saved
        /// Snapshot fields. Does not invalidate isolated frame spikes.
        /// </summary>
        public struct Stability
        {
            public int Frames;
            public float MedianMs;
            public float P95ToMedianRatio;
            public int SpikeFrames;
            public float SpikePercent;
            public bool HasEnoughSamples;
            public bool HasWarning;
            public string Explanation;
        }

        public Stability AnalyzeStability()
        {
            var result = new Stability { Frames = count };
            if (count < MinimumFrames)
            {
                result.Explanation = "Collect at least " + MinimumFrames +
                    " accepted frame intervals to inspect stability.";
                return result;
            }

            var values = new float[count];
            Array.Copy(frameMs, values, count);
            Array.Sort(values);
            // Even sample counts: average the two middle entries.
            int middle = count / 2;
            result.MedianMs = count % 2 == 0
                ? (float)(((double)values[middle - 1] + values[middle]) / 2d)
                : values[middle];
            float p95 = Percentile(values, .95f);
            result.P95ToMedianRatio = p95 / result.MedianMs;
            // A spike is strictly greater than twice the median; ties
            // at exactly 2x are not spikes.
            double threshold = 2d * result.MedianMs;
            foreach (var value in values)
                if (value > threshold) result.SpikeFrames++;
            result.SpikePercent = 100f * result.SpikeFrames / count;
            result.HasEnoughSamples = true;
            result.HasWarning = result.P95ToMedianRatio > 1.5f ||
                result.SpikePercent >= 5f;
            result.Explanation = result.HasWarning
                ? "Variable frame intervals detected. Inspect load and repeat " +
                  "the measurement; this is an advisory, not an automatic rejection."
                : "No large variability under the current advisory thresholds.";
            return result;
        }

        void OnEnable() => BeginFreshWindow();

        /// <summary>Discard previous measurements and allow a fresh warm-up window.</summary>
        public void BeginFreshWindow()
        {
            ResetSamples();
            warmupUntil = Time.realtimeSinceStartup + WarmupSeconds;
        }

        void Update()
        {
            if (IsWarmingUp) return;
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
