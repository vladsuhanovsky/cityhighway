# City3D outskirts texture: targeted startup optimization

## Measurements (Moscow, quality 1, Unity 6000.6.8f1)

Three raw user-supplied `city3d-generation-hotspots` CSV captures were collected after PR #73:

| Hotspot | Run 1 (ms) | Run 2 (ms) | Run 3 (ms) | Mean (ms) |
| --- | ---: | ---: | ---: | ---: |
| Outskirts ground | 989.92 | 988.06 | 999.22 | **992.40** |
| Terrain albedo bake | 873.76 | 843.53 | 854.47 | **857.25** |
| Terrain base pixel fill (nested) | 836.88 | 825.68 | 836.49 | **833.02** |
| Ground normal bake | 489.47 | 494.88 | 494.21 | **492.86** |
| Ground mesh upload and tangents | 19.72 | 13.39 | 14.73 | **15.95** |

The most expensive measured stage is **Outskirts ground**. The existing timing includes both `CityTextures.Outskirts` and mesh generation; the capture did not separate those costs. No optimization benefit is claimed without post-change measurements.

## Map-independent texture reuse

`CityTextures.Outskirts(CityMap map)` generated the same pixels regardless of `map`: it uses only fixed board dimensions, seeded noise, colors and world-coordinate transforms, and does not read properties from the supplied `CityMap`. Previously the texture was invalidated whenever a new `CityMap` instance was supplied, forcing a full **1024 × 1024** bake.

Now a live shared texture is returned for any map. This **can remove repeated baking between city generations within one Unity domain/session**. The public method retains its signature and the generated texture is unchanged. Unity destroyed-object null checks still trigger regeneration if the texture has been destroyed.

**Important:** entering a new Play Mode session with domain reload may clear the static cache. The first cold bake still runs, so repeated independent cold starts are not guaranteed to become much faster from caching alone.

## Safe early exit for fully hidden seam pixels

In the outskirts texture, pixel positions with `outside <= -0.05f` are entirely within the board. The existing blend factor `Clamp01((outside + 0.05f) / 0.7f)` is exactly zero, so their expensive grass, field, forest and village noise results would be discarded. The new early exit writes precisely `TerrainTexture.SeamColor` plus the same deterministic `Hash(px, py, 403)` grain as the original code.

Pixels outside this fully covered area retain the entire legacy noise and blending path. This optimization does **not** change world dimensions, the 3D city texture, other city-specific terrain, meshes or gameplay.

## Additional diagnostics

A new **`City3D.Hotspot/Outskirts texture bake`** CPU marker and matching **`Outskirts texture bake`** detailed-hotspot CSV sample are nested within the existing **`Outskirts ground`** sample. Compare parent duration with the nested texture sample to distinguish texture baking from mesh construction. Nested times must not be summed.

## Validation

- Unity Edit Mode CI must pass, including the new map-reuse and pixel-invariance tests.
- Visually check two different 3D cities and their outskirts, seams, riverbanks and tree placement.
- Start Moscow at quality 1 at least three times in a **single Unity Play Mode session**, then also test cold Play Mode reloads; save detailed hotspot and coarse generation-stage CSVs for both cases.
- Inspect whether `Outskirts texture bake` is large on first generation and small on subsequent city generations. The size of any improvement depends on cache lifetime and test conditions.
