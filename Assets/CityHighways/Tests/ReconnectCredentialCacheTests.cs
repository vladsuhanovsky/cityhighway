using CityHighways.Net;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class ReconnectCredentialCacheTests
    {
        [Test]
        public void KeyIsRandomAndWellFormed()
        {
            string first = ReconnectCredentialCache.NewKey();
            Assert.IsTrue(ReconnectCredentialCache.IsValid(first));
            Assert.AreNotEqual(first, ReconnectCredentialCache.NewKey());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("nickname")]
        [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaag")]
        public void InvalidKeyIsRejected(string key) => Assert.IsFalse(ReconnectCredentialCache.IsValid(key));

        [Test]
        public void CredentialIsScopedByProfileAccountAndDestination()
        {
            var cache = new ReconnectCredentialCache();
            var a = new object();
            var b = new object();
            string key = cache.For(a, "accountA", "lan:host");
            Assert.AreEqual(key, cache.For(a, "accountA", "lan:host"));
            Assert.AreNotEqual(key, cache.For(b, "accountA", "lan:host"));
            Assert.AreNotEqual(key, cache.For(a, "accountB", "lan:host"));
            Assert.AreNotEqual(key, cache.For(a, "accountA", "lan:other"));
            Assert.AreNotEqual(key, new ReconnectCredentialCache().For(a, "accountA", "lan:host"));
        }
    }
}
