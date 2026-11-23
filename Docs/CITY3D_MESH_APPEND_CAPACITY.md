# Reserving capacity when appending procedural meshes

`MeshBuilder.Append` merges the source builder into the destination after applying a transform.

## Implementation

- Before copying, reserve destination vertex, normal, UV and index list capacities for exactly the appended elements. List contents are untouched by reservation.
- Reject null source builders and self-append explicitly. Previously self-append iterated a list while expanding it.
- Empty source builders return without matrix inversion or buffer growth.
- Keep existing vertex transform, inverse-transpose normal transform, UV order, triangle indices and winding.
- Guard arithmetic sums against integer overflow before allocation.

## Validation

Run the Unity Edit Mode suite, including `City3DMeshAppendTests` (five tests covering index offsets, transforms, no-op empty append, invalid sources, repeated append and Clear reuse). Check a fixed city seed visually for changed normals and missing roofs or structures.

Profiler comparison can validate managed allocations during city generation. This is a targeted allocation optimization, **not** a measured FPS improvement. An exact-capacity strategy can still allocate a new array on each append; repeated append patterns should be profiled before adopting a larger growth strategy.

No save schema, gameplay rules, locale strings, or performance benchmark formats change.
