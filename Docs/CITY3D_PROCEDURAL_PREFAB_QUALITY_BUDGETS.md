# Procedural Prefab budgets — shared across generated city visuals

## Purpose
The existing procedural **building**, **tree** and **prop** Prefab rules can instantiate many models on a random Logistic Magnate map. This optional component limits automatic instances and geometry without changing the generator, logical map, save files or existing rules when it is absent.

## Authoring
1. In the startup scene choose **GameObject → City Highways → Procedural Prefab Budget**.
2. Leave **Map Name** empty to apply to all maps, including random Magnate seeds, or set an exact city name.
3. Set per-model **Max Triangles Per Prefab** (default 12000) and **Max Meshes Per Prefab** (default 12). Set **Max Total Triangles** (default 120000) across accepted automatic Prefab instances in the generated city.
4. Set shared instance counts for **Low Quality** (8), **Medium Quality** (24) and **High Quality** (48). These are caps on successful automatic replacements across **all three kinds together**, not independent caps for each rule.
5. Inspect the Budget component: it checks configured rule models in loaded scenes and shows warnings when an individual model exceeds mesh or triangle limits.

## Behavior
- **No Budget component** means the same legacy procedural rules as before, preserving previous appearances and maximum counts.
- The scene creates one shared budget session before automatic building, tree and prop Prefabs are spawned. Manually placed replacement anchors are processed first and are intentionally excluded from these optional caps.
- A candidate exceeding per-prefab meshes, per-prefab triangles, cumulative triangles or quality instance cap is skipped; the original procedural building/tree/prop stays in the plan and continues to render normally.
- The first rejected use of each Prefab logs a diagnostic warning showing mesh/triangle cost and used instance count; repeated candidates don't spam the log.
- A per-Prefab or total triangle threshold of **0** disables that specific geometry limit. A quality instance cap of **0** disables all automatic rule Prefab instances at that quality.
- Triangle counts are based on triangle-topology mesh index counts divided by 3, including MeshFilters and SkinnedMeshRenderers in Prefab children. Static estimates exclude actual GPU draw-call and FPS measurement.
- Existing deterministic selection, per-rule instance cap, seeded random-map support, distant tree billboards and district visibility are unchanged.

## Validation
1. Run Unity Edit Mode CI with `CityProceduralPrefabBudgetTests`, `Magnate3DCompatibilityTests` and the existing three rule-system suites.
2. With no Budget object, verify old ordinary maps and random Magnate maps render as before.
3. Add low quality cap 1, medium 2 and high 3 and use visible building/tree/prop Prefabs with Probability 1. Verify the **combined** count respects the selected limit.
4. Configure a per-Prefab mesh or triangle limit below a model's real cost. Verify Inspector warning, runtime log and fallback to original procedural geometry.
5. Confirm manual stable-ID Prefab overrides remain unchanged.
6. Profile runtime CPU, FPS and draw calls on multiple random Magnate seeds to set realistic limits.

Note: Geometry thresholds are guardrails, not auto-calibrated platform budgets. Choosing stricter caps can reduce model variety on slower graphics quality settings.