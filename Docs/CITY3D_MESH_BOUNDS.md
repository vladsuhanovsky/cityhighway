# Single-pass mesh bounds calculation

## Change

Both `MeshBuilder.ToMesh()` and `MeshBuilder.WriteTo()` submit triangle indices with `calculateBounds: false`, then call `Mesh.RecalculateBounds()` once after all vertex attributes have been populated. Previously `SetTriangles` calculated bounds implicitly, followed by an explicit second calculation.

This keeps the same mesh vertex positions, normals, UVs, triangle order, index format, and resulting bounds. It affects procedural meshes constructed by City3D only; no gameplay, saves or benchmark formats change.

## Validation

- Run Unity Edit Mode tests, particularly `City3DMeshBoundsTests` (creation, replacing large geometry by small, empty buffer) and existing terrain / tree mesh tests.
- Visually compare a fixed city seed and camera angle before and after the change for terrain, roads, buildings and billboard clipping.
- Inspect the Unity Profiler for CPU time attributed to mesh generation; performance benefit is expected in repeated mesh creation, but has **not been measured** and must not be asserted without profiling.
