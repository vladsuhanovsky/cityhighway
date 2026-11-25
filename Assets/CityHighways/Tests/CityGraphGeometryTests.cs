using System.Linq;
using CityHighways.Core;
using NUnit.Framework;

namespace CityHighways.Tests
{
    public class CityGraphGeometryTests
    {
        static CityMap Map(Location[] points, params Road[] roads) =>
            new CityMap("fixture", new[] { new District("d", "D") }, points, roads, new Contract[0]);
        static Location P(string id, float x, float y) => new Location(id, id, "d", x, y);
        static Road R(string id, string a, string b) => new Road(id, a, b, 1, Resource.Asphalt);

        [Test]
        public void DisjointRoadsCannotCross()
        {
            var map = Map(new[] { P("a",0,0), P("b",1,1), P("c",0,1), P("d",1,0) },
                R("r1","a","b"), R("r2","c","d"));
            Assert.Contains("road-crossing:r1:r2", CityGraphGeometry.Check(map));
        }

        [Test]
        public void RoadCannotPassThroughUnrelatedLocation()
        {
            var map = Map(new[] { P("a",0,0), P("b",1,1), P("c",0.5f,0.5f) }, R("r1","a","b"));
            Assert.Contains("road-through-location:r1:c", CityGraphGeometry.Check(map));
        }

        [Test]
        public void JunctionIsNotCrossing()
        {
            var map = Map(new[] { P("a",0,0), P("b",1,1), P("c",1,0) },
                R("r1","a","b"), R("r2","b","c"));
            Assert.IsEmpty(CityGraphGeometry.Check(map));
        }

        [Test]
        public void CollinearOverlapAtJunctionIsDetected()
        {
            var map = Map(new[] { P("a",0,0), P("b",1,1), P("c",0.5f,0.5f) },
                R("r1","a","b"), R("r2","a","c"));
            Assert.IsTrue(CityGraphGeometry.Check(map).Any(x => x.StartsWith("road-through-location:")));
        }
    }
}
