using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SoundBankCompletenessTests
    {
        [Test]
        public void EveryDefinedSoundEffectHasOneGeneratedClip()
        {
            var bank = new SoundBank(null);
            try
            {
                foreach (Sfx sound in Enum.GetValues(typeof(Sfx)))
                    Assert.IsTrue(bank.HasClip(sound), "Missing generated clip: " + sound);
                Assert.IsTrue(bank.HasClip(Sfx.Undo));
                Assert.IsTrue(bank.HasClip(Sfx.Redo));
            }
            finally
            {
                bank.Dispose();
            }
        }

        [Test]
        public void GeneratedClipsAreReleasedAndDisposeIsIdempotent()
        {
            var bank = new SoundBank(null);
            Assert.IsTrue(bank.HasClip(Sfx.Undo));
            bank.Dispose();
            Assert.IsFalse(bank.HasClip(Sfx.Undo));
            Assert.IsFalse(bank.HasClip(Sfx.Redo));
            Assert.DoesNotThrow(bank.Dispose);
        }
    }
}
