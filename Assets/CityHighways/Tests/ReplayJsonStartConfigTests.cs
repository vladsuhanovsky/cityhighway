using System;
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class ReplayJsonStartConfigTests
    {
        [Test]
        public void ExplicitGameConfigurationIsPresent()
        {
            Assert.IsTrue(ReplayJsonStartConfig.HasExplicitConfig(
                "{\"start\":{\"players\":[\"A\",\"B\"],\"config\":{\"turnLimit\":42}},\"history\":[{\"kind\":\"pass\"}]}"));
        }

        [TestCase("{}")]
        [TestCase("{\"start\":{}}")]
        [TestCase("{\"start\":{\"config\":null}}")]
        [TestCase("{\"start\":{\"config\":false}}")]
        [TestCase("{\"start\":{\"config\":[]}}")]
        [TestCase("{\"start\":{\"config\":\"{}\"}}")]
        [TestCase("{\"config\":{},\"start\":{}}")]
        [TestCase("{\"history\":[{\"start\":{\"config\":{}}}],\"start\":{}}")]
        [TestCase("{\"start\":{},\"author\":\"config\"}")]
        public void RejectsMissingNullOrNestedWrongConfiguration(string json)
        {
            Assert.IsFalse(ReplayJsonStartConfig.HasExplicitConfig(json));
        }

        [Test]
        public void EscapedLiteralStringCannotMasqueradeAsObject()
        {
            Assert.IsFalse(ReplayJsonStartConfig.HasExplicitConfig(
                "{\"start\":{\"config\":\"{\\\"notAnObject\\\":true}\"}}"));
        }

        [Test]
        public void RejectsDuplicateStartOrConfigFields()
        {
            Assert.IsFalse(ReplayJsonStartConfig.HasExplicitConfig(
                "{\"start\":{\"config\":{}},\"start\":{\"config\":{}}}"));
            Assert.IsFalse(ReplayJsonStartConfig.HasExplicitConfig(
                "{\"start\":{\"config\":{},\"config\":{}}}"));
        }

        [TestCase("{\"start\":{\"config\":{}}")]
        [TestCase("{\"start\":{\"config\":{}},}")]
        [TestCase("{\"start\":{\"config\":{},}}")]
        [TestCase("{\"start\":{\"config\":{}},\"history\":[null,]}")]
        [TestCase("{\"start\":{\"config\":{}},\"author\":\"bad\\x\"}")]
        public void RejectsMalformedJsonInsteadOfAcceptingSubstringMatches(string json)
        {
            Assert.IsFalse(ReplayJsonStartConfig.HasExplicitConfig(json));
        }

        [Test]
        public void EmbeddedStringTokensCannotChangeFieldNesting()
        {
            Assert.IsTrue(ReplayJsonStartConfig.HasExplicitConfig(
                "{\"description\":\"start: {\\\"config\\\": null}\",\"start\":{\"config\":{}},\"history\":[]}"));
        }

        [Test]
        public void RejectsMissingOrTooLongInput()
        {
            Assert.IsFalse(ReplayJsonStartConfig.HasExplicitConfig(null));
            Assert.IsFalse(ReplayJsonStartConfig.HasExplicitConfig(""));
            Assert.IsFalse(ReplayJsonStartConfig.HasExplicitConfig(
                new string('x', Replays.MaxCloudReplayJsonBytes + 1)));
        }

        [Test]
        public void SkipsLargeHistoryWithoutArrayMemberLimit()
        {
            var history = new System.Text.StringBuilder();
            for (int i = 0; i < 300; i++)
            {
                if (i > 0) history.Append(',');
                history.Append("{\"kind\":\"pass\",\"player\":0}");
            }
            string json = "{\"start\":{\"config\":{}},\"history\":[" + history + "]}";
            Assert.IsTrue(ReplayJsonStartConfig.HasExplicitConfig(json));
        }

        [Test]
        public void StartConfigCanAppearAfterLargeReplayHistory()
        {
            string json = "{\"history\":[{\"kind\":\"pass\"},{\"kind\":\"pass\"}]," +
                "\"start\":{\"players\":[\"A\",\"B\"],\"config\":{\"maxTurns\":10}}}";
            Assert.IsTrue(ReplayJsonStartConfig.HasExplicitConfig(json));
        }
    }
}
