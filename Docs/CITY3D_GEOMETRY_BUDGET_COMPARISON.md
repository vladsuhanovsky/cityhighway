# Runtime geometry budgets, baseline comparison and CSV export

Open **City Highways → 3D Objects → Scene Quality Audit**, start a 3D city in Play Mode and click **Capture actual generated mesh geometry**.

## Budget warnings
- **Total triangle budget** (default 300000): warning when total resident mesh triangles exceed the configured threshold.
- **Per-group triangle budget** (default 50000): warning and `[OVER BUDGET]` marker when any generated root mesh group exceeds this limit.
- Enter **0** for either budget to disable that warning. These thresholds are user-selected investigation aids; they are not measured FPS or validated hardware limits.

## Baseline versus current
After Capture, select **Save current capture as baseline** to retain the current totals and group data in the editor window. Start the same city with a different graphics quality (or change city), press **Capture actual generated mesh geometry** again, and review signed deltas for triangles, vertices, meshes and category triangle counts. The heaviest current group is also compared against the baseline by its name. A different-city comparison carries an explicit warning; totals are not like-for-like.

Snapshots exist only in the current EditorWindow session; they are not persisted on disk and do not affect gameplay.

## CSV export
Click **Export current geometry CSV...** to write all captured groups, not only those visible in Top geometry groups. The file contains City, Group, Category, Meshes, Vertices, Triangles, GroupBudget, OverBudget, and BaselineDeltaTriangles. CSV is UTF-8 with BOM, RFC-style quotes, numeric invariant formatting, and formula-injection escaping shared with the existing procedural audit exporter. Baseline delta is left blank unless a same-city baseline exists.

## Limitations
Triangle and vertex counts cover resident generated mesh objects, including inactive mesh representations and authored visuals under the generated root. They are not draw calls, per-frame visible triangles, FPS, GPU timing or hardware-dependent GPU memory.
Procedural buildings and trees are batched into district meshes and tree chunks, so ranking and comparison refer to **groups**, not individual procedural objects. Different quality settings can change group membership, and names are not guaranteed to be unique. No source Prefab or runtime mesh is edited by these controls.

## Acceptance
- [ ] Unity Edit Mode CI passes.
- [ ] Capture the same city in two quality settings, save the first as baseline and confirm signed deltas.
- [ ] Lower both triangle budgets to trigger warnings, then set 0 to disable warnings.
- [ ] Export CSV and check all groups, Cyrillic names and numeric data.
- [ ] Export with a different-city baseline; comparison shows a warning and CSV delta column remains blank.
- [ ] Verify original Scene Quality Audit and runtime capture still work.