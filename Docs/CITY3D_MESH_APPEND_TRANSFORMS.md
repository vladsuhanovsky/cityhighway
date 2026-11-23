# Mesh append transform fast path

When merging a procedural mesh, an exact identity or pure translation 4x4 matrix does not require the inverse-transpose normal matrix. `MeshBuilder.Append` now skips matrix inversion, translates vertices with direct vector addition, and copies input normals through `SafeNormal` for that narrowly detected case. Branching is outside the vertex loop.

Transforms with rotation, scale, shear, perspective-row values or any other deviation still use the previous inverse-transpose path. All points continue through `MultiplyPoint3x4`, with the same UV and triangle order.

## Validation

- Unity Edit Mode: verify `City3DMeshAppendTests` for identity, negative and positive translations, repeated mixed appends, and rotated/nonuniformly scaled meshes, along with existing mesh tests.
- Visual smoke test using a fixed 3D city seed: check roofs, structural details and lighting after generation.
- Profile frequent mesh merge calls to estimate CPU impact. No performance improvement is claimed until measured.

No saves, game rules, localization or benchmark formats are modified.
