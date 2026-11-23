# Runtime 3D tree-card mesh-buffer reuse

The generated 3D city refreshes some tree-card billboard meshes when the camera rotates or changes tilt. Previously each refresh built a brand-new `MeshBuilder` and four managed lists, populated them, wrote to an existing Unity `Mesh`, and discarded the builder. The updated implementation keeps one scratch builder per tree chunk and clears and refills its existing buffers.

## Changes

- `MeshBuilder(int expectedQuads = 0)` reserves List capacities for 4 vertices/normals/UVs and 6 triangle indices per quad. The no-argument constructor continues to work unchanged.
- Each 3D tree chunk lazily initializes its own `CardBuilder` using the number of visible tree-card spots and reuses it for later refreshes.
- Before adding geometry, `RebuildCards` invokes `Clear()`. It then uses exactly the same `QuadWithNormal` calls and `WriteTo(chunk.CardMesh)` as before. The mesh contents and camera-facing math are intended to remain unchanged.
- Existing visibility and card refresh policies are unchanged; no additional rebuilding is introduced.
- Four Edit Mode tests cover initial geometry, clearing and rewriting existing meshes, backward-compatible constructor behavior and repeated rebuilds without accumulating geometry.

## Performance claims and validation

This removes **per-refresh managed list and builder allocations** after the first refresh of each tree chunk, assuming the number of spots stays within the reserved capacity. It does not eliminate Unity's internal `Mesh.SetVertices/SetTriangles` costs, promise a measured FPS gain, pool meshes across districts or alter mesh culling.

Validate in the Unity Profiler using the same city and camera sequence before and after: observe GC Alloc in `City3DScene.RebuildCards`, frame-time P95 and the rendered billboard appearance. Inspect materials, tree colors and visibility while rotating and zooming.

## Compatibility

This change does not affect the random city generator, gameplay, save data, benchmark JSON schemas, user-facing translations, or the existing construction-prefab pool.
