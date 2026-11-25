using CityHighways.Net;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class ClientRecoveryTests
    {
        [Test]
        public void SubmitTimeoutKeepsPendingAndRequestsSyncNotSubmit()
        {
            var recovery = new ClientRecovery();
            recovery.BeginSubmit(10);
            Assert.IsTrue(recovery.Pending);
            Assert.IsFalse(recovery.Synchronizing);
            Assert.AreEqual(ClientRecovery.NextAction.None, recovery.Tick(15.99));
            Assert.AreEqual(ClientRecovery.NextAction.RequestSync, recovery.Tick(16));
            Assert.IsTrue(recovery.Pending);
            Assert.IsTrue(recovery.Synchronizing);
        }

        [Test]
        public void SyncAttemptsAreBoundedAndDuplicateRequestsDoNotExtendDeadline()
        {
            var recovery = new ClientRecovery();
            Assert.IsTrue(recovery.BeginSync(0));
            Assert.IsFalse(recovery.BeginSync(5));
            Assert.AreEqual(ClientRecovery.NextAction.RequestSync, recovery.Tick(6));
            Assert.AreEqual(ClientRecovery.NextAction.None, recovery.Tick(11.99));
            Assert.AreEqual(ClientRecovery.NextAction.Close, recovery.Tick(12));
            Assert.IsFalse(recovery.Pending);
            Assert.AreEqual(ClientRecovery.NextAction.None, recovery.Tick(100));
        }

        [Test]
        public void CompletionResetsWaitAndRetryBudget()
        {
            var recovery = new ClientRecovery();
            recovery.BeginSync(0);
            recovery.Tick(6);
            recovery.Complete();
            Assert.IsFalse(recovery.Pending);
            Assert.IsFalse(recovery.Synchronizing);
            Assert.AreEqual(ClientRecovery.NextAction.None, recovery.Tick(100));
            Assert.IsTrue(recovery.BeginSync(100));
            Assert.AreEqual(ClientRecovery.NextAction.RequestSync, recovery.Tick(106));
        }
    }
}
