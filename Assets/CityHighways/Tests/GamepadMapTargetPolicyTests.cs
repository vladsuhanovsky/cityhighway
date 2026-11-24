using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class GamepadMapTargetPolicyTests
    {
        [Test]
        public void HumanWithNoBlockersCanTargetMap()
        {
            Assert.IsTrue(GamepadMapTargetPolicy.AllowsInput(
                true, false, false, false, false));
        }

        [Test]
        public void CannotTargetDuringBotTurn()
        {
            Assert.IsFalse(GamepadMapTargetPolicy.AllowsInput(
                false, false, false, false, false));
        }

        [Test]
        public void EachBlockingConditionIndividuallyDisablesTargeting()
        {
            Assert.IsFalse(GamepadMapTargetPolicy.AllowsInput(
                true, true, false, false, false));
            Assert.IsFalse(GamepadMapTargetPolicy.AllowsInput(
                true, false, true, false, false));
            Assert.IsFalse(GamepadMapTargetPolicy.AllowsInput(
                true, false, false, true, false));
            Assert.IsFalse(GamepadMapTargetPolicy.AllowsInput(
                true, false, false, false, true));
        }

        [Test]
        public void MultipleBlockersNeverReenableTargeting()
        {
            Assert.IsFalse(GamepadMapTargetPolicy.AllowsInput(
                true, true, true, true, true));
        }
    }
}
