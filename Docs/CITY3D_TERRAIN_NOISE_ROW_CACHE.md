# Reuse deterministic value-noise corners while baking the 3D terrain texture

## Measured context (Moscow, quality 1, Unity 6000.6.8f1)

Three user-supplied hotspot exports after PR #74 confirm the expected cache behavior:

| Hotspot | Run 1 (ms) | Run 2 (ms) | Run 3 (ms) | Mean (ms) |
| --- | ---: | ---: | ---: | ---: |
| Outskirts ground | 996.27 | 298.41 | 302.39 | 532.36 |
| Outskirts texture bake | 689.76 | 0.0054 | 0.0036 | 229.92 |
| Terrain base pixel fill | 832.97 | 852.24 | 833.16 | **839.46** |
| Terrain albedo bake | 871.47 | 873.95 | 852.19 | 865.87 |
| Ground normal bake | 480.39 | 527.45 | 499.13 | 502.32 |

The outskirts texture is cached on the second and third starts within this measured session. Those repeat builds each spent about 300 ms building outskirts geometry. **Cold-start** outskirts time remains about 996 ms. This PR does not claim to optimize the outskirts geometry or cold-start texture.

With outskirts texture reuse confirmed, **terrain base pixel fill** is the next expensive repeatable hotspot. The baseline mean is ~839 ms.

## Optimization

`TerrainTexture.Bake` generates **1736 × 967 = 1,678,712** base pixels for City3D, sampling five value-noise layers per pixel:

- Grass noise at cell sizes 90, 34 and 11 (salts 11, 12, 13).
- Field noise at cell size 70 (salt 21).
- Pavement noise at cell size 40 (salt 31).

Each original `ValueNoise` recomputed four hashed noise corners for each pixel even when neighboring pixels shared the same grid cell.

`TerrainNoiseRowSampler` initializes one row's Y interpolation once and caches its four grid-corner hashes until X crosses a cell boundary. It preserves the **same hash constants, interpolation expression, FBM weights and seed values**. The 3D base-pixel loop uses five independent row samplers. The flat **2D terrain path continues to use its original `Fbm` and `ValueNoise` calls**.

This is a deterministic work-reduction optimization, not a change to terrain color, map layout, scenery distribution, shader, save data or quality settings. No multithreading and no change in texture resolution are introduced.

## Correctness and measurement

- Edit Mode regression tests compare optimized samples against the **original copied noise equations** at many rows, cell boundaries, negative coordinates, nonsequential requests and weighted grass combinations.
- Existing city terrain tests continue to validate texture creation, cache behavior and 2D/3D separation.
- After successful CI, visually compare city ground and parks with the prior version, especially edge blending and the gray central urban areas.
- Collect at least three new **detailed hotspot CSVs** for Moscow, quality 1 and the same Unity version/hardware. Compare `Terrain base pixel fill` and `Terrain albedo bake` before and after. Do not add nested timings to parent stage timings, and do not infer FPS improvements from startup numbers.

A faster CPU noise sampler is expected to reduce hash work, but the amount of wall-clock improvement must be measured in Unity, including memory/cache effects and Edit Mode overhead.
