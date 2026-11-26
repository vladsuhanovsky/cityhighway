using NUnit.Framework;
using UnityEngine;
using CityHighways.Presentation;
using Object = UnityEngine.Object;

namespace CityHighways.Tests
{
    public sealed class FleetVehicleVisualTests
    {
        static Material NewMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.IsNotNull(shader);
            return new Material(shader);
        }

        // MaterialPropertyBlock color round-trips may incur tiny floating-point
        // differences (including color-space conversion). Compare every channel,
        // not exact float bits; keep the tolerance far below palette differences.
        static void AssertPaint(Color expected, Color actual, string context)
        {
            const float tolerance = 0.0001f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), context + " (R)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), context + " (G)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), context + " (B)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance), context + " (A)");
        }

        [TestCase("paver", "Screed")]
        [TestCase("mixer", "Mixing drum")]
        [TestCase("grader", "Grading blade")]
        [TestCase("crane", "Boom")]
        public void FactoryCreatesDistinctBoundedVisualWithoutPhysics(string id, string feature)
        {
            var parent = new GameObject("Fleet test parent") { layer = 31 };
            var material = NewMaterial();
            try
            {
                var value = FleetLivery.Read(null, null, id);
                var model = FleetVehicleVisual.Build(value, material, parent.transform);
                Assert.IsNotNull(model);
                Assert.AreEqual(id, model.Vehicle);
                Assert.AreSame(parent.transform, model.Root.transform.parent);
                Assert.IsNotNull(model.Root.transform.Find(feature));
                Assert.IsEmpty(model.Root.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(model.Root.GetComponentsInChildren<Rigidbody>(true));
                Assert.IsEmpty(model.Root.GetComponentsInChildren<MonoBehaviour>(true));
                var renderers = model.Root.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.InRange(10, 64));
                foreach (var renderer in renderers)
                {
                    Assert.AreSame(material, renderer.sharedMaterial);
                    Assert.AreEqual(31, renderer.gameObject.layer);
                    Assert.Less(renderer.bounds.size.magnitude, 4f);
                }
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(material); }
        }

        [Test]
        public void RepaintDoesNotChangeSharedMaterialOrOtherVehicleAndRejectsMismatchedType()
        {
            var parent = new GameObject("Fleet paint test");
            var material = NewMaterial();
            try
            {
                var book = new ProfileBook();
                var a = new PlayerProfile { name = "Alpha" };
                var b = new PlayerProfile { name = "Beta" };
                CompanyIdentity.Store(book, a, CompanyIdentity.Create(a, "A", "", "green", "white"));
                CompanyIdentity.Store(book, b, CompanyIdentity.Create(b, "B", "", "blue", "steel"));
                var first = FleetVehicleVisual.Build(FleetLivery.Read(book, a, "mixer"), material, parent.transform);
                var second = FleetVehicleVisual.Build(FleetLivery.Read(book, b, "mixer"), material, parent.transform);
                int colorId = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
                Color original = material.GetColor(colorId);
                Color ColorOf(FleetVehicleVisual visual, string name)
                {
                    var renderer = visual.Root.transform.Find(name).GetComponent<Renderer>();
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    return block.GetColor(colorId);
                }
                AssertPaint(CompanyIdentity.Paint("green"), ColorOf(first, "Cab"), "Initial first cab");
                AssertPaint(CompanyIdentity.Paint("steel"), ColorOf(second, "Door stripe"), "Initial second stripe");
                var tire = ColorOf(first, "Tire");
                Assert.IsTrue(first.Apply(FleetLivery.Preview(book, a, "mixer", "reverse", false)));
                AssertPaint(CompanyIdentity.Paint("white"), ColorOf(first, "Cab"), "Repainted first cab");
                AssertPaint(CompanyIdentity.Paint("green"), ColorOf(first, "Door stripe"), "Repainted first stripe");
                AssertPaint(tire, ColorOf(first, "Tire"), "Mechanical tire unchanged");
                Assert.IsFalse(first.Apply(FleetLivery.Read(book, b, "crane")));
                AssertPaint(CompanyIdentity.Paint("white"), ColorOf(first, "Cab"), "First cab unchanged after rejected type");
                AssertPaint(CompanyIdentity.Paint("green"), ColorOf(first, "Door stripe"), "First stripe unchanged after rejected type");
                AssertPaint(CompanyIdentity.Paint("steel"), ColorOf(second, "Door stripe"), "Second stripe unchanged");
                AssertPaint(CompanyIdentity.Paint("blue"), ColorOf(second, "Cab"), "Second cab unchanged");
                Assert.AreEqual(original, material.GetColor(colorId));
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(material); }
        }

        [Test]
        public void MissingMaterialOrLiveryDoesNotCreateObjects()
        {
            var parent = new GameObject("Fleet invalid inputs");
            var material = NewMaterial();
            try
            {
                Assert.IsNull(FleetVehicleVisual.Build(null, material, parent.transform));
                Assert.IsNull(FleetVehicleVisual.Build(FleetLivery.Read(null, null, "crane"), null, parent.transform));
                Assert.AreEqual(0, parent.transform.childCount);
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(material); }
        }

        [Test]
        public void ActionLiveryRequiresExplicitVerifiedLocalOwner()
        {
            var book = new ProfileBook();
            var a = new PlayerProfile { name = "Alpha" };
            var b = new PlayerProfile { name = "Beta" };
            book.profiles.Add(a); book.profiles.Add(b); book.active = a.name;
            CompanyIdentity.Store(book, a, CompanyIdentity.Create(a, "A", "", "green", "white"));
            CompanyIdentity.Store(book, b, CompanyIdentity.Create(b, "B", "", "blue", "steel"));
            FleetLivery.Store(book, b, "crane", "reverse", false);
            var actual = FleetLivery.ForActionOwner(book, b, "crane");
            Assert.AreEqual("B", actual.CompanyName);
            Assert.AreEqual(CompanyIdentity.Paint("steel"), actual.Primary);
            Assert.AreEqual("", FleetLivery.ForActionOwner(book, null, "crane").CompanyName);
            Assert.AreEqual("", FleetLivery.ForActionOwner(book, new PlayerProfile { name = "Beta" }, "crane").CompanyName);
            Assert.AreEqual(CompanyIdentity.Paint("navy"), FleetLivery.ForActionOwner(book, null, "crane").Primary);
            Assert.IsNull(FleetLivery.ForActionOwner(book, b, "unknown"));
        }
    }
}
