# Reserving capacity when appending procedural meshes

`MeshBuilder.Append` merges the source builder into the destination after applying a transform.

## Implementation

- Before copying, ensure destination vertex, normal, UV and index list capacities can hold the appended elements. Capacity grows geometrically (doubling where possible, or to the required size for a large append); list contents are untouched. This amortizes repeated small appends.
- Reject null source builders and self-append explicitly. Previously self-append iterated a list while expanding it.
- Empty source builders return without matrix inversion or buffer growth.
- Keep existing vertex transform, inverse-transpose normal transform, UV order, triangle indices and winding.
- Guard arithmetic sums against integer overflow before allocation.

## Validation

Run the Unity Edit Mode suite, including `City3DMeshAppendTests` (five tests covering index offsets, transforms, no-op empty append, invalid sources, repeated append and Clear reuse). Check a fixed city seed visually for changed normals and missing roofs or structures.

Profiler comparison can validate managed allocations during city generation. This is a targeted allocation optimization, **not** a measured FPS improvement. Geometric growth retains spare capacity and may use more peak memory; benchmark repeated append patterns to determine the actual allocation/time impact. Tests verify that successive small merges can reuse capacity.

No save schema, gameplay rules, locale strings, or performance benchmark formats change.
