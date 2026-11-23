# 3D Scene Quality Audit (authored visuals)

Open **City Highways → 3D Objects → Scene Quality Audit** in Unity Edit Mode. Enter the exact city map name, set a minimum overlap depth (default **0.01 world units**) and click **Analyze authored 3D visuals**.

This on-demand editor tool scans active and enabled **CitySceneDecoration** and **CityProceduralReplacement** components in **loaded scenes** for the specified city. It does not instantiate Prefabs or change the scene.

## Three related measurements

1. **Decoration density**: total valid authored visual anchors divided by the full generated city footprint, **14 × 7.8 world units**. The tool also reports invalid/missing Prefab geometry and anchors skipped because they belong to other cities, are inactive, or are disabled.
2. **Potential 3D overlaps**: world-axis-aligned mesh bounding boxes are built from each source Prefab's combined local mesh bounds and the anchor's world Transform. A sweep through the sorted X axis counts pairs with intersection exceeding the chosen depth on **all three axes**. Up to 60 example pairs are displayed for manual inspection. Merely touching box faces does not count.
3. **Estimated rendering geometry**: number of valid visual anchors, mesh component instances, and the sum of triangle-submesh indices divided by three. These are **static geometry estimates**, not unique GPU meshes, real draw calls, real FPS, GPU milliseconds or GPU memory use.

The counts cover **authored replacement and decoration Prefabs only**. Generated buildings/trees, roads, terrain, particles, dynamic lights and the actual runtime scene are **not** measured. Two overlapping boxes can represent valid visual arrangements (for example, a model with empty space in its bounds). Scene View/Play Mode verification is necessary.

## Acceptance checks

- [ ] CI Unity Edit Mode tests pass.
- [ ] Open an authored city scene with several decorations and replacements; run the audit for its city.
- [ ] Duplicate/overlap two visual bounds and confirm the potential pair count increases.
- [ ] Move one model vertically beyond another and confirm that overlap disappears.
- [ ] Disable an anchor or change its city and confirm it is skipped.
- [ ] Try a missing/flat Prefab and confirm an invalid-geometry count rather than a crash.
- [ ] Confirm expected triangle/mesh estimates and that no scene modification or automatic fix takes place.
- [ ] Evaluate actual performance in Unity Profiler and Frame Debugger separately.
