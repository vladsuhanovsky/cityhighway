# Seed-independent building Prefab rules for random Logistic Magnate cities

## What this enables

Fixed `StableObjectId` anchors are useful for hand-authored maps but move when a randomly generated city has a different seed. This opt-in feature instead lets you select building Prefabs by procedural **building kind** and apply them automatically to generated cities of any seed.

## Setup

1. Open the Unity startup scene. Use **GameObject → City Highways → Procedural Building Prefab Rules** to create a rule-set object.
2. Leave **Map Name** empty to apply the rules to **every generated city**, including all Logistic Magnate seeds and normal maps; or specify one exact `CityMap.Name` to restrict a rule set.
3. In **Rules**, add entries for the desired **Building Kind** with **Visual Prefab**, **Probability** (0–1), **Prefab Scale** and **Max Instances**.
4. Keep each prefab's authored pivot and size appropriate for the procedural building type. The prefab is placed at the generated building center and at the highest terrain corner of its original footprint, using the original building yaw. Scale is an explicit multiplier rather than automatic geometry fitting.
5. Enter Play Mode and generate several different Logistic Magnate cities. Use the Scene Hierarchy to inspect `Generated <Kind> (rule)` objects under `City Highways 3D`.

## Precedence, compatibility and limits

- The rule set is **opt-in**. With no enabled matching component, rendering uses the existing procedural buildings exactly as before.
- Artist-authored explicit Prefab replacements are resolved **first**, followed by procedural rules and finally normal district meshes. Rules only consider buildings still present in the plan and thus cannot override a successful manual replacement.
- A generated building is selected deterministically from its seed and field position. Neither list iteration order nor game session randomness is used to draw the selection. The selection can differ across seeds, but is stable for the same generated building and probability.
- Rules are evaluated in their displayed order. The first eligible kind/probability rule whose `Max Instances` is not reached wins; a cap of 0 disables that entry. Counts are capped independently per rule. Keep caps modest because each Prefab becomes its own GameObject instead of being merged into the district mesh.
- Multiple active rule components for the same city are ambiguous and safely **disable automatic rules** with a warning.
- Automatic replacement keeps the generated building and footprint collections synchronized and does **not** modify the logical map, roads, districts, station locations, gameplay, saves or replay data.
- **Only buildings are supported in this first stage**. Tree billboard LOD uses a different batching path and is not replaced by these rules.
- These rules are **not performance optimization by themselves**: excessive authored Prefab instances may increase draw calls. Compare low/medium/high quality in Unity Profiler and the geometry audit.

## Manual acceptance

1. Run Unity Edit Mode CI including `CityProceduralBuildingPrefabRulesTests` and existing `Magnate3DCompatibilityTests`.
2. With no rule-set object, verify generated cities render identically.
3. Add a matching rule with an obvious visual Prefab, low probability and low Max Instances; generate Magnate maps with seed 1, 42, 2026.
4. Verify replacement counts never exceed Max Instances and stay stable for the same seed, city and quality.
5. Verify a manual Stable Object ID replacement takes precedence.
6. Disable the rule-set component or enter a different Map Name, restart the city and confirm original procedural meshes return.
7. Measure FPS, draw calls, visual placement on slopes and full 3D/2D mode transitions.