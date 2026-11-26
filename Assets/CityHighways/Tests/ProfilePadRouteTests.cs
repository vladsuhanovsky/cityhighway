using NUnit.Framework;
using UnityEngine;
using CityHighways.Presentation;

namespace CityHighways.Tests
{
    public sealed class ProfilePadRouteTests
    {
        static readonly Vector3 A = new Vector3(0f,.0035f,0f);
        static readonly Vector3 B = A + Vector3.right * .09f;
        static readonly Vector3 C = A + Vector3.forward * .008f;
        static readonly Vector3 D = B + Vector3.forward * .008f;

        [Test]
        public void LoopUsesAllFourEdgesAtConstantDistanceAndWraps()
        {
            var route = ProfilePadRoute.TryCreate(A,B,C,D);
            Assert.IsNotNull(route);
            Assert.That(route.Length, Is.EqualTo(.196f).Within(.00001f));
            void Check(float distance, Vector3 expected, Vector3 direction)
            {
                Assert.IsTrue(route.TrySample(distance, out var point, out var heading));
                Assert.That(Vector3.Distance(expected,point), Is.LessThan(.00001f));
                Assert.That(Vector3.Dot(heading * Vector3.forward,direction), Is.GreaterThan(.999f));
            }
            Check(.045f,(A+B)*.5f,Vector3.right);
            Check(.094f,(B+D)*.5f,Vector3.forward);
            Check(.143f,(D+C)*.5f,Vector3.left);
            Check(.192f,(C+A)*.5f,Vector3.back);
            Check(route.Length+.045f,(A+B)*.5f,Vector3.right);
            Assert.IsFalse(route.TrySample(float.NaN,out _,out _));
        }

        [Test]
        public void RejectsDisconnectedSlopedReversedOrInvalidLanes()
        {
            Assert.IsNull(ProfilePadRoute.TryCreate(A,B,C+Vector3.up*.005f,D+Vector3.up*.005f));
            Assert.IsNull(ProfilePadRoute.TryCreate(A,B,C+Vector3.forward*.03f,D+Vector3.forward*.03f));
            Assert.IsNull(ProfilePadRoute.TryCreate(A,B,D,C));
            Assert.IsNull(ProfilePadRoute.TryCreate(A,A,C,D));
            Assert.IsNull(ProfilePadRoute.TryCreate(new Vector3(float.NaN,0,0),B,C,D));
            Assert.IsNull(ProfilePadRoute.TryCreate(A,B,A,B));
        }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(270f)]
        public void CloseupOrbitsFeetAndLooksAtAvatarCenter(float yaw)
        {
            var feet = new Vector3(1f,.2f,2f);
            Assert.IsTrue(City3DScene.TryCharacterCloseupPose(feet,yaw,1f,out var point,out var rotation));
            var target = feet + Vector3.up * CityProfileWalker.Height * .55f;
            Assert.That(Vector3.Dot(rotation * Vector3.forward,(target-point).normalized), Is.GreaterThan(.999f));
            Assert.That(point.y, Is.GreaterThan(target.y));
            Assert.IsTrue(City3DScene.TryCharacterCloseupPose(feet,yaw,100f,out var near,out _));
            Assert.IsTrue(City3DScene.TryCharacterCloseupPose(feet,yaw,1.4f,out var clamped,out _));
            Assert.That(Vector3.Distance(near,clamped),Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(near,target),Is.LessThan(Vector3.Distance(point,target)));
        }

        [Test]
        public void CloseupRejectsNonFiniteInput()
        {
            Assert.IsFalse(City3DScene.TryCharacterCloseupPose(A,float.NaN,1f,out _,out _));
            Assert.IsFalse(City3DScene.TryCharacterCloseupPose(A,0f,float.PositiveInfinity,out _,out _));
            Assert.IsFalse(City3DScene.TryCharacterCloseupPose(new Vector3(0,float.NaN,0),0f,1f,out _,out _));
        }
    }
}
