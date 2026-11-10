using System;
using System.Collections.Generic;
using UnityEngine;

namespace RailGame.Presentation.App
{
    public enum Sfx
    {
        Click,
        Card,
        Swish,
        Clack,
        Whistle,
        Ticket,
        Error,
        Bell,
        Fanfare
    }

    /// <summary>
    /// Звуки, синтезированные кодом при запуске, — без единого аудиофайла.
    /// Позже их легко заменить настоящими сэмплами: достаточно подменить клипы в словаре.
    /// </summary>
    public sealed class SoundBank
    {
        const int SampleRate = 44100;

        readonly AudioSource _source;
        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly System.Random _noise = new System.Random(12345);

        public bool Enabled = true;
        public float Volume = 0.8f;

        public SoundBank(AudioSource source)
        {
            _source = source;

            _clips[Sfx.Click] = Make("click", 0.05f, t =>
                Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 70f) * 0.35f);

            _clips[Sfx.Card] = Make("card", 0.11f, t =>
                Noise() * Mathf.Sin(Mathf.PI * t / 0.11f) * 0.22f
                + Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 80f) * 0.05f);

            _clips[Sfx.Swish] = Make("swish", 0.25f, t =>
                Noise() * Mathf.Sin(Mathf.PI * t / 0.25f) * 0.18f);

            // «Тук» колёс: низкий удар со спадом частоты плюс короткий щелчок.
            _clips[Sfx.Clack] = Make("clack", 0.09f, t =>
                Mathf.Sin(2f * Mathf.PI * (150f - 500f * t) * t) * Mathf.Exp(-t * 40f) * 0.55f
                + Noise() * Mathf.Exp(-t * 110f) * 0.25f);

            // Паровозный гудок: две ноты с лёгким вибрато и «дыханием».
            _clips[Sfx.Whistle] = Make("whistle", 0.65f, t =>
            {
                float envelope = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.65f - t) / 0.2f);
                float vibrato = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                float tone = Mathf.Sin(2f * Mathf.PI * 587f * vibrato * t) + 0.8f * Mathf.Sin(2f * Mathf.PI * 740f * vibrato * t)
                             + 0.25f * Mathf.Sin(2f * Mathf.PI * 1174f * vibrato * t);
                return (tone * 0.17f + Noise() * 0.03f) * envelope;
            });

            _clips[Sfx.Ticket] = Make("ticket", 0.45f, t => Arpeggio(t, new[] { 523f, 659f, 784f }, 0.1f, 0.25f));
            _clips[Sfx.Fanfare] = Make("fanfare", 1.2f, t => Arpeggio(t, new[] { 523f, 659f, 784f, 1046f }, 0.16f, 0.25f));

            _clips[Sfx.Error] = Make("error", 0.18f, t =>
                Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 165f * t)) * Mathf.Clamp01((0.18f - t) / 0.06f) * 0.12f);

            _clips[Sfx.Bell] = Make("bell", 0.7f, t =>
                (Mathf.Sin(2f * Mathf.PI * 880f * t) + 0.45f * Mathf.Sin(2f * Mathf.PI * 1760f * t) * Mathf.Exp(-t * 8f))
                * Mathf.Exp(-t * 5.5f) * 0.25f);
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (!Enabled || _source == null) return;
            if (_clips.TryGetValue(sfx, out var clip)) _source.PlayOneShot(clip, volume * Volume);
        }

        float Noise() => (float)(_noise.NextDouble() * 2.0 - 1.0);

        static float Arpeggio(float t, float[] notes, float step, float tail)
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * step;
                if (t < start) continue;
                float local = t - start;
                bool last = i == notes.Length - 1;
                float decay = Mathf.Exp(-local * (last ? 3.5f : 9f));
                // Треугольная волна звучит мягче синуса и не так резко, как квадрат.
                float phase = notes[i] * local;
                float triangle = 4f * Mathf.Abs(phase - Mathf.Floor(phase + 0.5f)) - 1f;
                sum += triangle * decay * Mathf.Clamp01(local / 0.01f);
            }
            return sum * tail;
        }

        static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int length = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[length];
            for (int i = 0; i < length; i++)
                data[i] = Mathf.Clamp(wave(i / (float)SampleRate), -1f, 1f);

            var clip = AudioClip.Create("sfx_" + name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
