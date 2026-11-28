#if UNITY_EDITOR
using CityHighways.Presentation;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public sealed class ProfileRoadWorkerModularTrialTests
    {
        [TestCase(false, false, "M02_V9_Workwear.glb")]
        [TestCase(true, false, "F01_V9_Workwear.glb")]
        [TestCase(false, true, "M02_V9_RestoredBase.glb")]
        [TestCase(true, true, "F01_V9_RestoredBase.glb")]
        public void ModularPathsAreSeparateFromAcceptedDemo(bool female, bool restored, string file)
        {
            Assert.AreEqual("Assets/CityHighwaysLicensedArt/RoadWorkerV9/" + file,
                ProfileRoadWorkerTrial.ModularAssetPath(female, restored));
            Assert.AreNotEqual(ProfileRoadWorkerTrial.AssetPath(female, false),
                ProfileRoadWorkerTrial.ModularAssetPath(female, restored));
        }

        [TestCase("M02_V9_Base_HeadHair", "Head / baked hair")]
        [TestCase("F01_V9_Base_Feet", "Feet")]
        [TestCase("M02_boot_L_BOUND_TRIAL", "Boots")]
        [TestCase("F01_boot_R_BOUND_TRIAL", "Boots")]
        [TestCase("F01_V9_Shoes", "Boots")]
        [TestCase("M02_radio_BOUND_TRIAL", "Radio")]
        [TestCase("M02_V9_BaseSegments", null)]
        [TestCase("LeftFoot", null)]
        [TestCase("other_M02_V9_Base_Feet", null)]
        [TestCase(null, null)]
        public void OnlyAuthoredVisualNodesIdentifyModules(string node, string expected)
        {
            Assert.AreEqual(expected, ProfileRoadWorkerTrial.ModularGroup(node));
        }
    }
}
#endif
