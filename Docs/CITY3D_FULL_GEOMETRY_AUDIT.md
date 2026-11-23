# Whole generated 3D scene — geometry snapshot

The existing **City Highways → 3D Objects → Scene Quality Audit** now includes a Play Mode section for inspecting actual generated meshes of the active city.

## Workflow
1. Start a city in 3D Play Mode.
2. Open **City Highways → 3D Objects → Scene Quality Audit**.
3. Click **Capture actual generated mesh geometry**.
4. Inspect city name, building/tree counts, mesh and vertex totals, triangle budgets and Top geometry groups (5–100 ranked entries).
5. Recapture after switching city or quality settings. The earlier **Analyze authored 3D visuals** stays an independent Edit Mode tool.

## Counted geometry
- All MeshFilter and SkinnedMeshRenderer components below the active generated 3D root, including inactive child GameObjects.
- Triangle topology submesh index counts divided by 3, plus vertices and mesh instances.
- Top-level GameObject groups: `District ` is Buildings; `Trees ` is Trees; `(replacement)` and `(authored)` identify authored visuals; otherwise Other.
- Triangle budget breakdown: building districts, tree chunks, authored replacements and decorations, and other generated meshes.
- Largest groups by triangle count appear first.

## Limitations
Procedural buildings are combined into district meshes and procedural trees are batched into chunks. **A per-object ranking would be invented**, so the report identifies the most expensive actual mesh groups instead. Both inactive and active tree representations are counted: this is resident mesh complexity, not visible triangles.

These values are **not FPS, draw calls, overdraw, GPU timings or VRAM usage**; measure runtime performance separately in Unity Profiler and Frame Debugger. A capture is read-only and does not create or change meshes, map assets, rules, saves, replay or gameplay. The active-scene handle clears on Dispose.

## Acceptance
- [ ] Unity Edit Mode CI passes.
- [ ] Start a 3D city and capture nonzero totals in Play Mode.
- [ ] Confirm District and Trees group rankings and the category triangle breakdown.
- [ ] Switch quality and recapture to compare totals.
- [ ] No active city: capture button disabled.
- [ ] Verify the pre-existing Edit Mode authored-visual audit remains functional.