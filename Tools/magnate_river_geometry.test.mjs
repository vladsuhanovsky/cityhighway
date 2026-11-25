import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = p => readFileSync(new URL("../" + p, import.meta.url), "utf8");
const policy = source("Assets/CityHighways/Core/CityRiverPolyline.cs");
const generator = source("Assets/CityHighways/Core/Logistics/CityGenerator.cs");
const scene = source("Assets/CityHighways/Presentation/City3D/City3DScene.cs");
const hubs = source("Assets/CityHighways/Presentation/City3D/HubPlacement.cs");
const relief = source("Assets/CityHighways/Presentation/City3D/Relief.cs");
const planner = source("Assets/CityHighways/Presentation/City3D/CityPlanner.cs");
const policyTests = source("Assets/CityHighways/Tests/CityRiverPolylineTests.cs");
const alignmentTests = source("Assets/CityHighways/Tests/Magnate3DRiverAlignmentTests.cs");
const formerTests = source("Assets/CityHighways/Tests/MagnateGeneratorTests.cs");
const workflow = source(".github/workflows/magnate-river-validation.yml");

test("dense polyline is identified using actual 1400x780 field scale", () => {
  assert.match(policy, /MapWidth = 1400f/);
  assert.match(policy, /MapHeight = 780f/);
  assert.match(policy, /DenseMaxStepPixels = 45f/);
  assert.match(policy, /DenseMinimumPoints = 20/);
  assert.match(policy, /public static bool IsAlreadySmoothed\(IReadOnlyList<MapPoint> river\)/);
});

test("classification refuses a sparse, gapped or malformed river", () => {
  assert.match(policy, /river\.Count < DenseMinimumPoints/);
  assert.match(policy, /squared > maxStep2/);
  assert.match(policy, /float\.IsNaN\(a\.X\)/);
  assert.match(policy, /float\.IsInfinity\(b\.Y\)/);
  assert.match(policyTests, /DenseCountCannotMaskAnUnexpectedLongGap/);
  assert.match(policyTests, /DegenerateOrMalformedPathIsNotSafeToClassifyAsPreSmoothed/);
});

test("generator saves the original densely sampled centerlines into map", () => {
  assert.match(generator, /var lines = rivers\.Select\(r => Smooth\(r\)\)\.ToList\(\)/);
  assert.match(generator, /map\.SetExtras\(lines\.Select/);
});

test("water validation never silently changes an already smoothed centerline", () => {
  const check = generator.slice(generator.indexOf("public static List<string> CheckWater("),
    generator.indexOf("// ================================================================== Геометрия"));
  assert.match(check, /CityRiverPolyline\.IsAlreadySmoothed\(r\) \? points : Smooth\(points\)/);
  assert.match(check, /foreach \(var location in map\.Locations\)/);
  assert.match(check, /if \(d < WaterHalfWidth \+ 10f\)/);
});

test("3D water uses exactly stored dense curve rather than re-smoothing it", () => {
  const prepare = scene.slice(scene.indexOf("void PrepareRiverLines()"),
    scene.indexOf("const float JoinDepth"));
  assert.match(prepare, /bool preSmoothed = CityRiverPolyline\.IsAlreadySmoothed\(line\)/);
  assert.match(prepare, /var smooth = preSmoothed \? raw : Smooth\(raw, 5\)/);
  assert.match(prepare, /minStep = preSmoothed \? 0\.00001f : MinRiverStep/);
});

test("sparse hand-authored rivers still retain their original smoothing behavior", () => {
  assert.match(scene, /const float MinRiverStep = 0\.15f/);
  assert.match(scene, /var smooth = preSmoothed \? raw : Smooth\(raw, 5\)/);
  assert.match(policyTests, /SparseAuthoredMapMustKeepLegacySmoothing/);
  assert.match(policyTests, /ExistingSparseCityRiversAreStillIdentifiedAsHandAuthored/);
});

test("extension beyond map edges happens after centerline preparation", () => {
  const prepare = scene.slice(scene.indexOf("void PrepareRiverLines()"),
    scene.indexOf("const float JoinDepth"));
  const smoothing = prepare.indexOf("var smooth = preSmoothed ? raw : Smooth(raw, 5)");
  const extension = prepare.indexOf("smooth.Insert(0,");
  assert.ok(smoothing >= 0 && extension > smoothing);
  assert.match(prepare, /startAtEdge \? 30f : half \* 0\.6f/);
});

test("scene hub placement reads the same map river geometry as safety validation", () => {
  assert.match(hubs, /foreach \(var line in map\.Rivers\)/);
  assert.match(hubs, /river\.Add\(\(World\(line\[i\]\.X, line\[i\]\.Y\), World\(line\[i \+ 1\]\.X, line\[i \+ 1\]\.Y\)\)\)/);
  assert.match(scene, /HubPlacement\.Plan\(_map, _hubSites, RiverWidth \/ 2f \+ BankWidth\)/);
});

test("city planner avoids river when placing buildings", () => {
  assert.match(planner, /Nearest\(p, river\) < 24f/);
  assert.match(planner, /NearHub\(p\)/);
});

test("river relief derives its valley from the final 3D mesh centerline", () => {
  assert.match(scene, /new ReliefField\(_map, _riverLines,/);
  assert.match(relief, /foreach \(var line in riverLines\)/);
});

test("regression tests verify stations, hubs and multiple random seeds", () => {
  assert.match(policyTests, /MagnateRiversAreAlreadyDenseAndPassWaterChecks/);
  assert.match(alignmentTests, /GeneratedHubSitesRemainOutsideSavedRiverFootprint/);
  assert.match(alignmentTests, /StationMarkersAreDryOnSaved3DWorldLine/);
  assert.match(formerTests, /WaterCheck_FindsStationInRiver/);
});

test("CI checks generator, 3D water and shared policy when they change", () => {
  for (const file of ["CityRiverPolyline.cs", "CityGenerator.cs",
      "City3DScene.cs", "Magnate3DRiverAlignmentTests.cs",
      "CityRiverPolylineTests.cs", "magnate_river_geometry.test.mjs"]) {
    assert.equal(workflow.split(file).length - 1, 2, file);
  }
});
