using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    public enum Sfx
    {
        Click,
        Card,
        Swish,
        Clack,
        Horn,
        Ticket,
        Error,
        Bell,
        Fanfare,
        Coin,
        /// <summary>Панель выезжает: короткий восходящий «вжух».</summary>
        Open,
        /// <summary>Панель уезжает: тот же «вжух» вниз.</summary>
        Close,
        /// <summary>Карусель встала на карточку: тихий щелчок.</summary>
        Tick,
        /// <summary>Везунчик: монетка подброшена и звенит, падая на стол.</summary>
        CharLucky,
        /// <summary>Прораб: три удара молотка по гвоздю.</summary>
        CharForeman,
        /// <summary>Страховщик: два щелчка замков портфеля.</summary>
        CharInsurer,
        /// <summary>Сборщик пошлин: касса «дзынь» и шорох монет в мешке.</summary>
        CharTollman,
        /// <summary>Чиновник: тяжёлый удар печати по бумаге.</summary>
        CharOfficial,
        /// <summary>Мостовик: гулкий звон стальной балки.</summary>
        CharBridger,
        /// <summary>Brief descending, quiet two-tone confirmation for undo.</summary>
        Undo
    }

    /// <summary>
    /// Звуки, синтезированные кодом при запуске, — без единого аудиофайла.
    /// Позже их легко заменить настоящими сэмплами: достаточно подменить клипы в словаре.
    /// </summary>
    public sealed class SoundBank : IDisposable
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

            // Укладка участка дороги: глухой удар катка плюс короткий щелчок.
            _clips[Sfx.Clack] = Make("clack", 0.09f, t =>
                Mathf.Sin(2f * Mathf.PI * (150f - 500f * t) * t) * Mathf.Exp(-t * 40f) * 0.55f
                + Noise() * Mathf.Exp(-t * 110f) * 0.25f);

            // Автомобильный гудок: два коротких «би-бип» на двух нотах с жёстким тембром.
            _clips[Sfx.Horn] = Make("horn", 0.42f, t =>
            {
                bool second = t > 0.2f;
                float local = second ? t - 0.2f : t;
                if (local > 0.16f) return 0f;
                float envelope = Mathf.Clamp01(local / 0.01f) * Mathf.Clamp01((0.16f - local) / 0.03f);
                float f = second ? 440f : 370f;
                float tone = Mathf.Sin(2f * Mathf.PI * f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * f * 1.26f * t)
                             + 0.3f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * 2f * t));
                return tone * 0.16f * envelope;
            });

            _clips[Sfx.Ticket] = Make("ticket", 0.45f, t => Arpeggio(t, new[] { 523f, 659f, 784f }, 0.1f, 0.25f));
            _clips[Sfx.Fanfare] = Make("fanfare", 1.2f, t => Arpeggio(t, new[] { 523f, 659f, 784f, 1046f }, 0.16f, 0.25f));

            _clips[Sfx.Error] = Make("error", 0.18f, t =>
                Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 165f * t)) * Mathf.Clamp01((0.18f - t) / 0.06f) * 0.12f);

            // Монетка: два коротких звонких «динь» — плата за проезд.
            _clips[Sfx.Coin] = Make("coin", 0.32f, t =>
            {
                float local = t < 0.08f ? t : t - 0.08f;
                float f = t < 0.08f ? 1568f : 2093f;
                return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-local * 18f) * 0.18f
                       + Mathf.Sin(2f * Mathf.PI * f * 2.7f * t) * Mathf.Exp(-local * 40f) * 0.05f;
            });

            // Панели: шум с плавной огибающей плюс скользящий по высоте тон — вверх при открытии, вниз при закрытии.
            _clips[Sfx.Open] = Make("open", 0.18f, t =>
                Noise() * Mathf.Sin(Mathf.PI * t / 0.18f) * 0.07f
                + Mathf.Sin(2f * Mathf.PI * (420f + 900f * t) * t) * Mathf.Sin(Mathf.PI * t / 0.18f) * 0.10f);
            _clips[Sfx.Close] = Make("close", 0.16f, t =>
                Noise() * Mathf.Sin(Mathf.PI * t / 0.16f) * 0.06f
                + Mathf.Sin(2f * Mathf.PI * (700f - 900f * t) * t) * Mathf.Sin(Mathf.PI * t / 0.16f) * 0.08f);
            _clips[Sfx.Tick] = Make("tick", 0.03f, t =>
                Mathf.Sin(2f * Mathf.PI * 2600f * t) * Mathf.Exp(-t * 160f) * 0.22f);

            // Action undo: soft descending signal, distinct from error and
            // original card sounds. Generated locally with all other SFX.
            _clips[Sfx.Undo] = Make("undo", 0.24f, t =>
            {
                float fade = Mathf.Clamp01((0.24f - t) / 0.10f);
                float pitch = 700f - 1200f * t;
                return (Mathf.Sin(2f * Mathf.PI * pitch * t) +
                        0.3f * Mathf.Sin(2f * Mathf.PI * pitch * 1.5f * t))
                       * Mathf.Exp(-t * 9f) * fade * 0.13f;
            });

            // Везунчик: щелчок большого пальца, звон вращающейся монетки и три затухающих «динь» при падении.
            _clips[Sfx.CharLucky] = Make("char_lucky", 0.9f, t =>
            {
                float flick = Noise() * Mathf.Exp(-t * 300f) * 0.3f;
                float spin = t < 0.45f ? Mathf.Sin(2f * Mathf.PI * 2637f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 18f * t)) * 0.05f * Mathf.Clamp01((0.45f - t) / 0.1f) : 0f;
                float drops = 0f;
                float[] at = { 0.45f, 0.6f, 0.7f };
                for (int i = 0; i < at.Length; i++)
                {
                    if (t < at[i]) continue;
                    float l = t - at[i];
                    drops += (Mathf.Sin(2f * Mathf.PI * 2093f * l) + 0.5f * Mathf.Sin(2f * Mathf.PI * 5274f * l)) * Mathf.Exp(-l * 22f) * (0.2f - i * 0.05f);
                }
                return flick + spin + drops;
            });

            // Прораб: три удара молотка — глухой удар с металлическим «тиньк» по гвоздю.
            _clips[Sfx.CharForeman] = Make("char_foreman", 0.75f, t =>
            {
                float sum = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float start = i * 0.22f;
                    if (t < start) continue;
                    float l = t - start;
                    sum += Mathf.Sin(2f * Mathf.PI * (190f - 300f * l) * l) * Mathf.Exp(-l * 45f) * 0.5f
                           + Noise() * Mathf.Exp(-l * 140f) * 0.3f
                           + Mathf.Sin(2f * Mathf.PI * 3100f * l) * Mathf.Exp(-l * 35f) * 0.08f;
                }
                return sum;
            });

            // Страховщик: замки портфеля — два сухих металлических щелчка и мягкий хлопок крышки.
            _clips[Sfx.CharInsurer] = Make("char_insurer", 0.5f, t =>
            {
                float sum = 0f;
                float[] at = { 0f, 0.12f };
                foreach (float start in at)
                {
                    if (t < start) continue;
                    float l = t - start;
                    sum += (Noise() * 0.6f + Mathf.Sin(2f * Mathf.PI * 4200f * l)) * Mathf.Exp(-l * 260f) * 0.35f
                           + Mathf.Sin(2f * Mathf.PI * 1800f * l) * Mathf.Exp(-l * 90f) * 0.08f;
                }
                if (t > 0.3f)
                {
                    float l = t - 0.3f;
                    sum += Noise() * Mathf.Exp(-l * 60f) * 0.12f + Mathf.Sin(2f * Mathf.PI * 120f * l) * Mathf.Exp(-l * 30f) * 0.25f;
                }
                return sum;
            });

            // Сборщик пошлин: касса — щелчок ящика, звонкое «дзынь» и шорох монет.
            _clips[Sfx.CharTollman] = Make("char_tollman", 0.9f, t =>
            {
                float drawer = Noise() * Mathf.Exp(-t * 80f) * 0.2f + Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 25f) * 0.2f;
                float bell = t > 0.06f
                    ? (Mathf.Sin(2f * Mathf.PI * 1975f * (t - 0.06f)) + 0.4f * Mathf.Sin(2f * Mathf.PI * 3950f * (t - 0.06f))) * Mathf.Exp(-(t - 0.06f) * 5f) * 0.16f
                    : 0f;
                float coins = 0f;
                for (int i = 0; i < 5; i++)
                {
                    float start = 0.25f + i * 0.07f + (i % 2) * 0.02f;
                    if (t < start) continue;
                    float l = t - start;
                    coins += Mathf.Sin(2f * Mathf.PI * (2600f + i * 340f) * l) * Mathf.Exp(-l * 45f) * 0.06f;
                }
                return drawer + bell + coins;
            });

            // Чиновник: печать — замах (короткий шорох) и тяжёлый удар по столу с бумагой.
            _clips[Sfx.CharOfficial] = Make("char_official", 0.55f, t =>
            {
                float swing = t < 0.12f ? Noise() * Mathf.Sin(Mathf.PI * t / 0.12f) * 0.08f : 0f;
                if (t < 0.12f) return swing;
                float l = t - 0.12f;
                return Mathf.Sin(2f * Mathf.PI * (95f - 80f * l) * l) * Mathf.Exp(-l * 14f) * 0.6f
                       + Noise() * Mathf.Exp(-l * 50f) * 0.3f;
            });

            // Мостовик: удар по стальной балке — низкий гул с биением обертонов.
            _clips[Sfx.CharBridger] = Make("char_bridger", 1.3f, t =>
            {
                float hit = Noise() * Mathf.Exp(-t * 120f) * 0.25f;
                float ring = (Mathf.Sin(2f * Mathf.PI * 220f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 563f * t)
                              + 0.45f * Mathf.Sin(2f * Mathf.PI * 1097f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 1811f * t))
                             * Mathf.Exp(-t * 3.2f) * 0.12f;
                return hit + ring;
            });

            _clips[Sfx.Bell] = Make("bell", 0.7f, t =>
                (Mathf.Sin(2f * Mathf.PI * 880f * t) + 0.45f * Mathf.Sin(2f * Mathf.PI * 1760f * t) * Mathf.Exp(-t * 8f))
                * Mathf.Exp(-t * 5.5f) * 0.25f);
        }

        /// <summary>Свой звук у каждого персонажа — для карточки выбора.</summary>
        public static Sfx ForCharacter(CityHighways.Core.CharacterKind kind)
        {
            switch (kind)
            {
                case CityHighways.Core.CharacterKind.Lucky: return Sfx.CharLucky;
                case CityHighways.Core.CharacterKind.Foreman: return Sfx.CharForeman;
                case CityHighways.Core.CharacterKind.Insurer: return Sfx.CharInsurer;
                case CityHighways.Core.CharacterKind.Tollman: return Sfx.CharTollman;
                case CityHighways.Core.CharacterKind.Official: return Sfx.CharOfficial;
                case CityHighways.Core.CharacterKind.Bridger: return Sfx.CharBridger;
                default: return Sfx.Card;
            }
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (!Enabled || _source == null) return;
            if (_clips.TryGetValue(sfx, out var clip)) _source.PlayOneShot(clip, volume * Volume);
        }

        /// <summary>Allows coverage of every synthesized SFX ID at Edit Mode time.</summary>
        public bool HasClip(Sfx sfx) => _clips.ContainsKey(sfx);

        public void Dispose()
        {
            foreach (var clip in _clips.Values)
            {
                if (clip == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(clip);
                else UnityEngine.Object.DestroyImmediate(clip);
            }
            _clips.Clear();
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
