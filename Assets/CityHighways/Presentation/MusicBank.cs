using System;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// An original, low-key harmonic city ambience. Generated at runtime so
    /// no licensed audio assets, network streaming or music packages are needed.
    /// Separate from short one-shot effects and their AudioSource.
    /// </summary>
    public static class MusicSoundscape
    {
        public const int SampleRate = 22050;
        public const float LoopSeconds = 16f;
        public const float DefaultVolume = 0.35f;

        // Four original four-second phrases. All frequencies are in Hz.
        static readonly float[] Roots = { 196f, 164.81f, 130.81f, 146.83f };

        public static bool ShouldPlay(bool musicEnabled, bool gameSoundEnabled, float level) =>
            musicEnabled && gameSoundEnabled && !float.IsNaN(level) &&
            !float.IsInfinity(level) && level > 0.001f;

        public static float SafeVolume(float level) =>
            float.IsNaN(level) || float.IsInfinity(level) ? 0f : Mathf.Clamp01(level);

        public static float Sample(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return 0f;
            float time = Mathf.Repeat(seconds, LoopSeconds);
            int phrase = Mathf.Clamp(Mathf.FloorToInt(time / 4f), 0, Roots.Length - 1);
            float local = time - phrase * 4f;
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(local / 0.35f));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((4f - local) / 0.35f));
            float envelope = Mathf.Min(attack, release);
            float root = Roots[phrase];

            // Gentle minor-third/fifth texture and a short tonal accent
            // at each phrase start. Both components fade to zero at edges,
            // so the 16-second AudioClip can repeat without a hard seam.
            float pad = (Mathf.Sin(2f * Mathf.PI * root * time) * 0.54f +
                         Mathf.Sin(2f * Mathf.PI * root * 1.1892f * time) * 0.27f +
                         Mathf.Sin(2f * Mathf.PI * root * 1.4983f * time) * 0.19f);
            float accent = Mathf.Sin(2f * Mathf.PI * root * 2f * local) *
                           Mathf.Exp(-local * 2.4f) * 0.065f * attack;
            return Mathf.Clamp(pad * envelope * 0.19f + accent * release, -1f, 1f);
        }
    }

    /// <summary>Lifecycle and volume for a dedicated looping music AudioSource.</summary>
    public sealed class MusicBank : IDisposable
    {
        readonly AudioSource _source;
        AudioClip _clip;

        public MusicBank(AudioSource source)
        {
            _source = source;
            if (_source == null) return;
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
        }

        public void Sync(bool enabled, float volume)
        {
            if (_source == null) return;
            float safe = MusicSoundscape.SafeVolume(volume);
            if (!enabled || safe <= 0.001f)
            {
                if (_source.isPlaying) _source.Stop();
                return;
            }
            _source.volume = safe;
            if (_source.isPlaying) return;
            if (_clip == null)
            {
                int samples = Mathf.RoundToInt(MusicSoundscape.LoopSeconds * MusicSoundscape.SampleRate);
                var data = new float[samples];
                for (int i = 0; i < samples; i++)
                    data[i] = MusicSoundscape.Sample(i / (float)MusicSoundscape.SampleRate);
                _clip = AudioClip.Create("cityhighways_original_city_ambience",
                    samples, 1, MusicSoundscape.SampleRate, false);
                _clip.SetData(data, 0);
            }
            _source.clip = _clip;
            _source.Play();
        }

        public void Dispose()
        {
            if (_source != null && _source.isPlaying) _source.Stop();
            if (_clip != null)
            {
                UnityEngine.Object.Destroy(_clip);
                _clip = null;
            }
        }
    }
}
