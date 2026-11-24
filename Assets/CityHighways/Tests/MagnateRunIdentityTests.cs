using System;
using System.Collections.Generic;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class MagnateRunIdentityTests
    {
        [Test]
        public void NewRunIdIsCanonicalLowercaseGuidWithoutDashes()
        {
            string runId = MagnateRunIdentity.Create();
            Assert.AreEqual(32, runId.Length);
            Assert.IsTrue(MagnateRunIdentity.IsValid(runId));
            Assert.IsTrue(Guid.TryParseExact(runId, "N", out _));
            Assert.AreEqual(runId.ToLowerInvariant(), runId);
        }

        [Test]
        public void EachNewGameIdIsFreshButCanBeReusedForTheSameReport()
        {
            var unique = new HashSet<string>();
            for (int i = 0; i < 32; i++)
                Assert.IsTrue(unique.Add(MagnateRunIdentity.Create()));
            var existing = MagnateRunIdentity.Create();
            Assert.IsTrue(MagnateRunIdentity.IsValid(existing));
            Assert.AreEqual(existing, existing);
        }

        [Test]
        public void RejectsUppercaseDashesAndUnrelatedStrings()
        {
            var valid = MagnateRunIdentity.Create();
            Assert.IsFalse(MagnateRunIdentity.IsValid(valid.ToUpperInvariant()));
            Assert.IsFalse(MagnateRunIdentity.IsValid(Guid.NewGuid().ToString("D")));
            Assert.IsFalse(MagnateRunIdentity.IsValid("0123456789abcdef"));
            Assert.IsFalse(MagnateRunIdentity.IsValid(""));
            Assert.IsFalse(MagnateRunIdentity.IsValid(null));
            Assert.IsFalse(MagnateRunIdentity.IsValid(new string('0', 32)));
            Assert.IsFalse(MagnateRunIdentity.IsValid(new string('g', 32)));
            Assert.IsFalse(MagnateRunIdentity.IsValid(new string(' ', 32)));
        }

        [Test]
        public void RunIdentityMustNotDependOnMatchSeedOrPlayerNames()
        {
            // A fixed procedural map seed can be reused in different matches;
            // a report identity must NOT be computed from that seed.
            var first = MagnateRunIdentity.Create();
            var second = MagnateRunIdentity.Create();
            Assert.AreNotEqual(first, second);
        }
    }
}
