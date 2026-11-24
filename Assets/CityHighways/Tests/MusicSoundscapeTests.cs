using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class MusicSoundscapeTests
    {
        [Test]
        public void SeamAndPhraseBoundariesFadeWithoutClicks()
        {
            Assert.AreEqual(0f, MusicSoundscape.Sample(0f), 0.0001f);
            Assert.AreEqual(0f, MusicSoundscape.Sample(4f), 0.0001f);
            Assert.AreEqual(0f, MusicSoundscape.Sample(8f), 0.0001f);
            Assert.AreEqual(0f, MusicSoundscape.Sample(12f), 0.0001f);
            Assert.AreEqual(0f, MusicSoundscape.Sample(MusicSoundscape.LoopSeconds), 0.0001f);
            Assert.Less(Mathf.Abs(MusicSoundscape.Sample(
                MusicSoundscape.LoopSeconds - 0.001f)), 0.001f);
        }

        [Test]
        public void MusicIsPeriodicAndDeterministic()
        {
            foreach (float t in new[] { 0.08f, 0.6f, 1.12f, 3.4f, 5.2f, 10.1f })
            {
                float first = MusicSoundscape.Sample(t);
                Assert.AreEqual(first, MusicSoundscape.Sample(t), 1e-6f);
                Assert.AreEqual(first, MusicSoundscape.Sample(t +
                    MusicSoundscape.LoopSeconds), 0.002f);
            }
        }

        [Test]
        public void AllSamplesStayFiniteAndInReasonableHeadroom()
        {
            for (int n = 0; n < 5000; n++)
            {
                float sample = MusicSoundscape.Sample(
                    MusicSoundscape.LoopSeconds * n / 5000f);
                Assert.IsFalse(float.IsNaN(sample));
                Assert.IsFalse(float.IsInfinity(sample));
                Assert.LessOrEqual(Mathf.Abs(sample), 0.3f);
            }
        }

        [Test]
        public void InvalidTimesProduceSilence()
        {
            Assert.AreEqual(0f, MusicSoundscape.Sample(float.NaN));
            Assert.AreEqual(0f, MusicSoundscape.Sample(float.PositiveInfinity));
            Assert.AreEqual(0f, MusicSoundscape.Sample(float.NegativeInfinity));
        }

        [Test]
        public void MutedGameOrZeroMusicVolumeNeverRequestsPlayback()
        {
            Assert.IsFalse(MusicSoundscape.ShouldPlay(false, true, 0.5f));
            Assert.IsFalse(MusicSoundscape.ShouldPlay(true, false, 0.5f));
            Assert.IsFalse(MusicSoundscape.ShouldPlay(true, true, 0f));
            Assert.IsFalse(MusicSoundscape.ShouldPlay(true, true, float.NaN));
            Assert.IsFalse(MusicSoundscape.ShouldPlay(true, true, float.PositiveInfinity));
            Assert.IsTrue(MusicSoundscape.ShouldPlay(true, true, 0.4f));
        }

        [Test]
        public void MusicVolumeIsSafeWithoutChangingGlobalMaster()
        {
            Assert.AreEqual(0f, MusicSoundscape.SafeVolume(-1f));
            Assert.AreEqual(1f, MusicSoundscape.SafeVolume(2f));
            Assert.AreEqual(0f, MusicSoundscape.SafeVolume(float.NaN));
            Assert.AreEqual(0f, MusicSoundscape.SafeVolume(float.PositiveInfinity));
            Assert.AreEqual(0.35f, MusicSoundscape.SafeVolume(0.35f), 1e-6f);
        }

        [Test]
        public void OldDisplaySettingsOptOutAndGetAudibleDefaultVolume()
        {
            var old = DisplaySettings.FromJson("{\"volume\":0.8,\"uiSounds\":true}");
            Assert.IsNotNull(old);
            Assert.IsFalse(old.musicEnabled);
            Assert.AreEqual(MusicSoundscape.DefaultVolume, old.musicVolume, 1e-6f);
        }

        [Test]
        public void ExplicitMusicMuteSurvivesSerializationRoundTrip()
        {
            var settings = new DisplaySettings { musicEnabled = true, musicVolume = 0f };
            string json = JsonUtility.ToJson(settings);
            var restored = DisplaySettings.FromJson(json);
            Assert.IsTrue(restored.musicEnabled);
            Assert.AreEqual(0f, restored.musicVolume, 1e-6f);
            Assert.IsFalse(MusicSoundscape.ShouldPlay(restored.musicEnabled, true,
                restored.musicVolume));
        }

        [Test]
        public void AudioVolumeMigrationClampsOutOfRangeAndKeepsOptIn()
        {
            var settings = DisplaySettings.FromJson(
                "{\"musicEnabled\":true,\"musicVolume\":1.5}");
            Assert.IsTrue(settings.musicEnabled);
            Assert.AreEqual(1f, settings.musicVolume, 1e-6f);
            Assert.IsNull(DisplaySettings.FromJson(null));
        }
    }
}
