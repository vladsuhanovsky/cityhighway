# MeshBuilder input validation and reuse

Two MeshBuilder entry points now reject invalid null arguments explicitly:

- `WriteTo(null)` throws `ArgumentNullException(nameof(mesh))` before trying to clear a Unity mesh.
- `Drape(null)` throws `ArgumentNullException(nameof(height))` before iterating over vertices.

Valid input behavior is unchanged. `WriteTo` still clears the existing Unity mesh, chooses the index format, writes vertices/normals/UVs/triangles and recalculates bounds. `Drape` still adds per-vertex height to y positions.

## Validation

The new `City3DMeshBuilderInputTests` exercises rejected null calls, valid height sampling and successive `WriteTo` invocations with cleared/recreated geometry. Existing terrain, billboard and mesh append Edit Mode tests remain in scope.

No save schema, UI, terrain generation rules or benchmark data are affected. This is an API safety and regression-test improvement, not an FPS optimization.
