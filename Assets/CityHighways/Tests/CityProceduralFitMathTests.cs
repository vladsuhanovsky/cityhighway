using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class CityProceduralFitMathTests
    {
        [Test]
        public void UniformFitUsesMostRestrictiveAxis()
        {
            Assert.IsTrue(CityProceduralFitMath.TryUniformFit(
                new Vector3(2f, 1f, 4f), Vector3.one,
                new Vector3(1f, 2f, 1f), out var factor));
            Assert.That(factor, Is.EqualTo(0.25f).Within(0.00001f));
        }

        [Test]
        public void FitAccountsForExistingNonUniformWorldScale()
        {
            Assert.IsTrue(CityProceduralFitMath.TryUniformFit(
                new Vector3(1f, 2f, 1f), new Vector3(2f, 0.5f, 3f),
                new Vector3(4f, 4f, 6f), out var factor));
            Assert.That(factor, Is.EqualTo(2f).Within(0.00001f));
        }

        [Test]
        public void NegativeMirrorScaleIsMeasuredAsAbsolute()
        {
            Assert.IsTrue(CityProceduralFitMath.TryUniformFit(
                Vector3.one, new Vector3(-2f, 1f, 1f),
                new Vector3(2f, 3f, 3f), out var factor));
            Assert.That(factor, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void FlatOrInvalidGeometryNeverChangesScale()
        {
            Assert.IsFalse(CityProceduralFitMath.TryUniformFit(
                new Vector3(1f, 0f, 1f), Vector3.one, Vector3.one, out var flatFactor));
            Assert.AreEqual(1f, flatFactor);
            Assert.IsFalse(CityProceduralFitMath.TryUniformFit(
                Vector3.one, Vector3.zero, Vector3.one, out var zeroFactor));
            Assert.AreEqual(1f, zeroFactor);
            Assert.IsFalse(CityProceduralFitMath.TryUniformFit(
                Vector3.one, Vector3.one, new Vector3(float.NaN, 1f, 1f), out _));
        }
    }
}
