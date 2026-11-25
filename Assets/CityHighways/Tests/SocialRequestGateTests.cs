using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SocialRequestGateTests
    {
        [Test]
        public void FreshGateDoesNotAcceptUnissuedToken()
        {
            var gate = new SocialRequestGate();
            Assert.IsFalse(gate.IsCurrent(0));
            Assert.IsFalse(gate.IsCurrent(1));
        }

        [Test]
        public void FirstRequestBecomesCurrent()
        {
            var gate = new SocialRequestGate();
            int token = gate.Begin();
            Assert.IsTrue(gate.IsCurrent(token));
        }

        [Test]
        public void NewerRequestInvalidatesEarlierResponse()
        {
            var gate = new SocialRequestGate();
            int first = gate.Begin();
            int second = gate.Begin();
            Assert.IsFalse(gate.IsCurrent(first));
            Assert.IsTrue(gate.IsCurrent(second));
        }

        [Test]
        public void InvalidateRejectsPendingCallback()
        {
            var gate = new SocialRequestGate();
            int pending = gate.Begin();
            gate.Invalidate();
            Assert.IsFalse(gate.IsCurrent(pending));
        }

        [Test]
        public void TwoLogicalResourcesHaveIndependentRequestVersions()
        {
            var friends = new SocialRequestGate();
            var club = new SocialRequestGate();
            int friendsToken = friends.Begin();
            int clubToken = club.Begin();
            friends.Begin();
            Assert.IsFalse(friends.IsCurrent(friendsToken));
            Assert.IsTrue(club.IsCurrent(clubToken));
        }

        [Test]
        public void ClubMutationInvalidatesOutstandingRefresh()
        {
            var club = new SocialRequestGate();
            int priorRefresh = club.Begin();
            int action = club.Begin();
            Assert.IsFalse(club.IsCurrent(priorRefresh));
            Assert.IsTrue(club.IsCurrent(action));
        }

        [Test]
        public void AccountSwitchInvalidatesBothRefreshFamilies()
        {
            var friends = new SocialRequestGate();
            var club = new SocialRequestGate();
            int oldFriends = friends.Begin();
            int oldClub = club.Begin();
            friends.Invalidate();
            club.Invalidate();
            Assert.IsFalse(friends.IsCurrent(oldFriends));
            Assert.IsFalse(club.IsCurrent(oldClub));
        }

        [Test]
        public void RepeatingRefreshAfterInvalidationAcceptsOnlyTheLast()
        {
            var gate = new SocialRequestGate();
            int old = gate.Begin();
            gate.Invalidate();
            int recent = gate.Begin();
            Assert.IsFalse(gate.IsCurrent(old));
            Assert.IsTrue(gate.IsCurrent(recent));
        }
    }
}
