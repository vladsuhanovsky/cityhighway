# Terrain mesh construction: reserve geometry buffers

The generated 3D city builds two large procedural terrain meshes at scene creation. Both previously started from empty `MeshBuilder` lists and expanded storage repeatedly as quads were appended.

## Changes

- **Main ground grid** contains precisely 240 × 136 quads. Its builder now allocates capacity for that many quads once, covering vertices, normals, UVs and indices.
- **Outskirts** starts with a conservative reservation for 120 × 120 base cells. River-adjacent cells may be subdivided into smaller quads, so Unity Lists grow if the estimate is insufficient. This is intentional and maintains exactly the original tessellation.
- The geometry generation order, quad vertices, normals, UVs, 32-bit mesh index handling and rendering remain unchanged.
- Four Edit Mode tests verify capacity-based grids, reservation underestimate and expansion, the >65K-vertex ground grid, and clearing/reusing the builder.

## How to validate

1. Open a fixed procedural city and choose the same map seed and quality in both revisions.
2. Profile startup `City3DScene.BuildGround` and `BuildOutskirtsGround`, especially managed GC Alloc, CPU frame time and peak memory.
3. Compare ground elevations, riverbanks, visible seams, triangulation and materials. Capture baseline and current measurements using the existing repeat journal.
4. Expect fewer transient List reallocations during terrain generation; **do not assume lower peak RAM or a measured FPS improvement without Profiler data**. Reserved capacity increases initial list allocations and may exceed final outskirts geometry in simple maps.

No changes to save-game schemas, gameplay, world map generation, benchmark JSON schemas or localization.
