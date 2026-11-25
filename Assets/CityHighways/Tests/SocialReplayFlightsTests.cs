using System;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class SocialReplayFlightsTests
    {
        static SocialCloud.RecordInfo Record(long id = 42, int score = 97) =>
            new SocialCloud.RecordInfo
            {
                id = id, mode = "classic", mapPath = "Maps/demo",
                mapName = "Demo", score = score, won = true, seats = 2
            };

        static ReplayFile Replay() =>
            new ReplayFile
            {
                start = new GameStartInfo(),
                mode = "classic", mapPath = "Maps/demo", map = "Demo",
                authorScore = 97, won = true,
                players = new[] { "Friend", "Bot" }
            };

        [Test]
        public void FirstSubscriberStartsRequest()
        {
            var queue = new SocialReplayFlights();
            Assert.IsTrue(queue.Begin(Record(), _ => { }, _ => { }, out int token));
            Assert.AreNotEqual(0, token);
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void SameRecordSharesOneRequestAndNotifiesBothCallbacks()
        {
            var queue = new SocialReplayFlights();
            int calls = 0;
            Assert.IsTrue(queue.Begin(Record(), _ => calls++, _ => Assert.Fail(), out int token));
            Assert.IsFalse(queue.Begin(Record(), _ => calls++, _ => Assert.Fail(), out int joinedToken));
            Assert.AreEqual(token, joinedToken);
            Assert.AreEqual(1, queue.Count);
            Assert.IsTrue(queue.Complete(42, token, Replay(), "mismatch"));
            Assert.AreEqual(2, calls);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void EverySubscriberMustMatchItsOwnRecordMetadata()
        {
            var queue = new SocialReplayFlights();
            int successes = 0, failures = 0;
            queue.Begin(Record(score: 97), _ => successes++, _ => failures++, out int token);
            queue.Begin(Record(score: 80), _ => successes++, _ => failures++, out _);
            Assert.IsTrue(queue.Complete(42, token, Replay(), "mismatch"));
            Assert.AreEqual(1, successes);
            Assert.AreEqual(1, failures);
        }

        [Test]
        public void FirstViewerCannotAlterLaterSubscribersMetadataDecision()
        {
            var queue = new SocialReplayFlights();
            int accepted = 0, rejected = 0;
            queue.Begin(Record(score: 97), replay =>
            {
                accepted++;
                // This callback mutates the shared replay, but must not
                // change the decision for subscribers validated earlier.
                replay.authorScore = 80;
            }, _ => Assert.Fail(), out int token);
            queue.Begin(Record(score: 97), _ => accepted++, _ => rejected++, out _);
            Assert.IsTrue(queue.Complete(42, token, Replay(), "mismatch"));
            Assert.AreEqual(2, accepted);
            Assert.AreEqual(0, rejected);
        }

        [Test]
        public void RecordMetadataIsCopiedWhenSubscriberJoins()
        {
            var queue = new SocialReplayFlights();
            int success = 0;
            var record = Record();
            queue.Begin(record, _ => success++, _ => Assert.Fail(), out int token);
            record.mapPath = "Maps/changed-after-click";
            Assert.IsTrue(queue.Complete(42, token, Replay(), "mismatch"));
            Assert.AreEqual(1, success);
        }

        [Test]
        public void DifferentRecordIdsStartIndependentRequests()
        {
            var queue = new SocialReplayFlights();
            int called = 0;
            Assert.IsTrue(queue.Begin(Record(42), _ => called++, _ => { }, out int first));
            Assert.IsTrue(queue.Begin(Record(43), _ => called++, _ => { }, out int second));
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(2, queue.Count);
            Assert.IsTrue(queue.Complete(43, second, Replay(), "mismatch"));
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, called);
            Assert.IsTrue(queue.Complete(42, first, Replay(), "mismatch"));
            Assert.AreEqual(2, called);
        }

        [Test]
        public void FailedHttpResponseReleasesAllSubscribersAndAllowsRetry()
        {
            var queue = new SocialReplayFlights();
            int failures = 0;
            queue.Begin(Record(), _ => Assert.Fail(), _ => failures++, out int token);
            queue.Begin(Record(), _ => Assert.Fail(), _ => failures++, out _);
            Assert.IsTrue(queue.Fail(42, token, "network"));
            Assert.AreEqual(2, failures);
            Assert.AreEqual(0, queue.Count);
            Assert.IsTrue(queue.Begin(Record(), _ => { }, _ => { }, out int retry));
            Assert.AreNotEqual(token, retry);
        }

        [Test]
        public void SwitchingSteamAccountCancelsAllPendingDownloads()
        {
            var queue = new SocialReplayFlights();
            int failures = 0;
            queue.Begin(Record(42), _ => Assert.Fail(), _ => failures++, out int old42);
            queue.Begin(Record(43), _ => Assert.Fail(), _ => failures++, out int old43);
            queue.Begin(Record(43), _ => Assert.Fail(), _ => failures++, out _);
            queue.CancelAll("account changed");
            Assert.AreEqual(3, failures);
            Assert.AreEqual(0, queue.Count);
            Assert.IsFalse(queue.Complete(42, old42, Replay(), "mismatch"));
            Assert.IsFalse(queue.Complete(43, old43, Replay(), "mismatch"));
        }

        [Test]
        public void AtoBtoAOldHttpResponseCannotFinishNewRequestWithSameId()
        {
            var queue = new SocialReplayFlights();
            int successes = 0;
            queue.Begin(Record(), _ => Assert.Fail(), _ => { }, out int oldToken);
            queue.CancelAll("account changed");
            queue.Begin(Record(), _ => successes++, _ => Assert.Fail(), out int newToken);
            Assert.AreNotEqual(oldToken, newToken);
            Assert.IsFalse(queue.Complete(42, oldToken, Replay(), "mismatch"));
            Assert.AreEqual(1, queue.Count);
            Assert.IsTrue(queue.Complete(42, newToken, Replay(), "mismatch"));
            Assert.AreEqual(1, successes);
        }

        [Test]
        public void InvalidRecordCannotStartOrLeavePendingRequest()
        {
            var queue = new SocialReplayFlights();
            Assert.IsFalse(queue.Begin(null, _ => Assert.Fail(), _ => { }, out int nil));
            Assert.AreEqual(0, nil);
            Assert.IsFalse(queue.Begin(Record(0), _ => Assert.Fail(), _ => { }, out int zero));
            Assert.AreEqual(0, zero);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void CallbackCanStartNewFlightAfterPreviousWasDetached()
        {
            var queue = new SocialReplayFlights();
            int second = 0;
            queue.Begin(Record(), _ =>
            {
                Assert.IsTrue(queue.Begin(Record(), r => second++, _ => { }, out int newToken));
                Assert.AreNotEqual(0, newToken);
            }, _ => Assert.Fail(), out int oldToken);
            Assert.IsTrue(queue.Complete(42, oldToken, Replay(), "mismatch"));
            Assert.AreEqual(1, queue.Count);
            // The detached old response can never complete the newly created request.
            Assert.IsFalse(queue.Complete(42, oldToken, Replay(), "mismatch"));
            Assert.AreEqual(0, second);
        }

        [Test]
        public void ThrowingFirstSubscriberCannotStarveSecondSubscriber()
        {
            var queue = new SocialReplayFlights();
            int called = 0;
            queue.Begin(Record(), _ => throw new InvalidOperationException("test callback"),
                _ => { }, out int token);
            queue.Begin(Record(), _ => called++, _ => { }, out _);
            Assert.IsTrue(queue.Complete(42, token, Replay(), "mismatch"));
            Assert.AreEqual(1, called);
        }

        [Test]
        public void CancelAllWithNoFlightIsSafeAndIdempotent()
        {
            var queue = new SocialReplayFlights();
            queue.CancelAll("account changed");
            queue.CancelAll("account changed");
            Assert.AreEqual(0, queue.Count);
        }
    }
}
