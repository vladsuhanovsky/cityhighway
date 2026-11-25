using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class JunctionCrosswalkLayoutTests
    {
        static Vector2 Direction(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        static List<Vector2> Select(List<Vector2> directions)
        {
            var result = new List<Vector2>();
            JunctionCrosswalkLayout.Select(directions, City3DScene.RoadWidth / 2f,
                City3DScene.CrosswalkNear, City3DScene.CrosswalkFar,
                result, new List<int>(), new List<int>());
            return result;
        }

        static void AssertNoOverlap(List<Vector2> selected)
        {
            for (int i = 0; i < selected.Count; i++)
                for (int j = i + 1; j < selected.Count; j++)
                    Assert.IsFalse(JunctionCrosswalkLayout.Overlap(selected[i], selected[j],
                        City3DScene.RoadWidth / 2f, City3DScene.CrosswalkNear, City3DScene.CrosswalkFar));
        }

        [Test]
        public void OrthogonalJunctionRetainsAllFourCrosswalks()
        {
            var dirs = new List<Vector2> { Direction(-90), Direction(0), Direction(90), Direction(180) };
            CollectionAssert.AreEqual(dirs, Select(dirs));
        }

        [Test]
        public void AcuteArmsRetainOneFullSizeCrosswalk()
        {
            var dirs = new List<Vector2> { Direction(0), Direction(8) };
            var selected = Select(dirs);
            Assert.AreEqual(1, selected.Count);
            Assert.AreEqual(dirs[0], selected[0]);
            AssertNoOverlap(selected);
        }

        [Test]
        public void TriesAlternativeStartingArmToRetainMoreMarkings()
        {
            // Central arm conflicts with each outer arm; outer arms fit each other.
            var dirs = new List<Vector2> { Direction(-40), Direction(0), Direction(40) };
            var selected = Select(dirs);
            Assert.AreEqual(2, selected.Count);
            CollectionAssert.AreEqual(new[] { dirs[0], dirs[2] }, selected);
            AssertNoOverlap(selected);
        }

        [Test]
        public void EmptyInputClearsPreviousResultAndScratchState()
        {
            var result = new List<Vector2> { Vector2.right };
            var scratch = new List<int> { 0 };
            var best = new List<int> { 0 };
            JunctionCrosswalkLayout.Select(new List<Vector2>(), .055f, .114f, .136f, result, scratch, best);
            Assert.IsEmpty(result);
            Assert.IsEmpty(best);
            Assert.IsEmpty(scratch);
        }

        [Test]
        public void DenseJunctionIsDeterministicAndRetainsOnlyOriginalDirections()
        {
            var dirs = Enumerable.Range(0, 24).Select(i => Direction(i * 15f - 180f)).ToList();
            var selected = Select(dirs);
            CollectionAssert.AreEqual(selected, Select(dirs));
            Assert.Greater(selected.Count, 1);
            Assert.Less(selected.Count, dirs.Count);
            Assert.IsTrue(selected.All(d => dirs.Contains(d)));
            AssertNoOverlap(selected);
        }

        [TestCase(-8)]
        [TestCase(0)]
        public void GeneratedCityCorpusHasNoSelectedCrosswalkEnvelopeOverlap(int first)
        {
            for (int seed = first; seed < first + 8; seed++)
            {
                var city = CityGenerator.Generate(seed);
                var layout = City3DLayoutAudit.Capture(city.Map, null);
                Assert.IsFalse(layout.warnings.Any(w => w.code == "crosswalk-envelope-overlap"), "seed " + seed);
                Assert.Greater(layout.crosswalksDrawn, 0, "seed " + seed);
                Assert.AreEqual(layout.crosswalkCandidates, layout.crosswalksDrawn + layout.crosswalksSuppressed);
            }
        }
    }
}
