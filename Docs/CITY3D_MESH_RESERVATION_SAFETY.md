# MeshBuilder quad reservation integer safety

`MeshBuilder(expectedQuads)` allocates arrays for four vertices, normals and UVs, and six triangle indices per reserved quad. Extremely large values could previously overflow the signed 32-bit index-list capacity multiplication and lead to misleading failures.

The constructor now throws `ArgumentOutOfRangeException` if `expectedQuads > int.MaxValue / 6`, before performing any allocations. Negative hints are still treated as zero for backwards compatibility. Normal terrain and billboard reservation counts are unchanged.

## Validation

- Unity Edit Mode: `City3DTerrainMeshCapacityTests` checks overflowing inputs are rejected and negative hints still allow meshes to grow.
- Existing 240 × 136 terrain geometry and 32-bit index tests continue to cover real city usage.
- This is an arithmetic safety guard, **not** a memory-budget limit. Very large *in-range* reservations can still cause out-of-memory errors; callers should derive realistic capacity estimates.

No gameplay, saves, shader or benchmark schema changes.
