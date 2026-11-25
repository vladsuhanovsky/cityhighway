using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SocialAccountScopeTests
    {
        const string SteamA = "76561198000000001";
        const string SteamB = "76561198000000002";

        [Test]
        public void FirstOwnerBecomesActiveAndAnUnchangedOwnerDoesNotIncrementEpoch()
        {
            var scope = new SocialAccountScope();
            Assert.IsNull(scope.Owner);
            Assert.IsFalse(scope.Update(null));
            Assert.IsTrue(scope.Update(SteamA));
            int revision = scope.Generation;
            Assert.IsTrue(scope.Matches(SteamA, revision));
            Assert.IsFalse(scope.Update(SteamA));
            Assert.AreEqual(revision, scope.Generation);
            Assert.IsTrue(scope.Matches(SteamA, revision));
        }

        [Test]
        public void SwitchingAccountsInvalidatesPendingCallbacksFromPriorOwner()
        {
            var scope = new SocialAccountScope();
            scope.Update(SteamA);
            int epochA = scope.Generation;
            scope.Update(SteamB);
            int epochB = scope.Generation;
            Assert.IsFalse(scope.Matches(SteamA, epochA));
            Assert.IsTrue(scope.Matches(SteamB, epochB));
            Assert.AreNotEqual(epochA, epochB);
        }

        [Test]
        public void ReturningToOldAccountNeverRevivesItsOldNetworkResponse()
        {
            var scope = new SocialAccountScope();
            scope.Update(SteamA);
            int previousA = scope.Generation;
            scope.Update(SteamB);
            scope.Update(SteamA);
            Assert.IsFalse(scope.Matches(SteamA, previousA));
            Assert.IsTrue(scope.Matches(SteamA, scope.Generation));
        }

        [Test]
        public void SteamOfflineInvalidatesPendingCallbacksAndClearsIdentity()
        {
            var scope = new SocialAccountScope();
            scope.Update(SteamA);
            int oldEpoch = scope.Generation;
            Assert.IsTrue(scope.Update(null));
            Assert.IsNull(scope.Owner);
            Assert.IsFalse(scope.Matches(SteamA, oldEpoch));
            Assert.IsFalse(scope.Matches(null, scope.Generation));
        }

        [Test]
        public void InvalidSteamIdIsNeverAcceptedAsAnOwner()
        {
            var scope = new SocialAccountScope();
            Assert.IsFalse(scope.Update("bad steam identity"));
            Assert.IsNull(scope.Owner);
            scope.Update(SteamA);
            Assert.IsTrue(scope.Update("invalid"));
            Assert.IsNull(scope.Owner);
        }

        [Test]
        public void InvalidIdsCannotRevalidateAnOldSession()
        {
            var scope = new SocialAccountScope();
            scope.Update(SteamA);
            int first = scope.Generation;
            scope.Update("garbage");
            scope.Update(SteamA);
            Assert.IsFalse(scope.Matches(SteamA, first));
        }

        [Test]
        public void NullAccountAndStaleEpochNeverMatchAValidAccount()
        {
            var scope = new SocialAccountScope();
            Assert.IsFalse(scope.Matches(SteamA, scope.Generation));
            scope.Update(SteamA);
            Assert.IsFalse(scope.Matches(SteamB, scope.Generation));
            Assert.IsFalse(scope.Matches(SteamA, scope.Generation - 1));
        }

        [Test]
        public void EmptyAndNullOwnerAreTheSameSignedOutState()
        {
            var scope = new SocialAccountScope();
            Assert.IsFalse(scope.Update(""));
            Assert.IsFalse(scope.Update(null));
            scope.Update(SteamA);
            Assert.IsTrue(scope.Update(""));
            int epoch = scope.Generation;
            Assert.IsFalse(scope.Update(null));
            Assert.AreEqual(epoch, scope.Generation);
        }
    }
}
