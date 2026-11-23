# MeshBuilder 16/32-bit index format transitions

Unity mesh generation uses 16-bit indices for small geometry and switches to 32-bit indices when more than 65,000 vertices are present. `MeshBuilder.WriteTo` clears and rewrites an existing Mesh, which can be reused for procedurally rebuilt objects.

## Regression coverage

Two Edit Mode tests exercise transitions using the same Unity Mesh and MeshBuilder:

1. Small mesh (UInt16) → large mesh with 66,040 vertices (UInt32) → small mesh (UInt16), confirming triangle counts and final bounds.
2. Large mesh (UInt32) → empty mesh (UInt16) → small mesh (UInt16), checking that stale geometry is removed.

These tests verify existing behavior; they **do not** change mesh production code, appearance, gameplay, save formats, or performance. The large tests may use more managed memory during the Edit Mode test run than ordinary small-mesh tests.

## Acceptance

Run the Unity Edit Mode CI and review any failures or memory constraints. If they pass, this establishes a regression baseline for future mesh regeneration and index format optimizations.
