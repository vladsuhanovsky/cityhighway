# Procedural tree Prefab rules for random Logistic Magnate cities

## Purpose
Seed-independent, opt-in tree Prefab rules extend the existing procedural building rules. The original generated tree meshes and billboard LOD remain unchanged unless a valid tree rule intentionally selects individual trees.

## Setup
1. In the Unity startup scene choose **GameObject → City Highways → Procedural Tree Prefab Rules**.
2. Leave **Map Name** empty for every generated map, including random Logistic Magnate seeds, or enter an exact city name.
3. Add **Rules** entries and set **Tree Kind**, **Visual Prefab**, **Probability** (0 to 1), **Prefab Scale**, and **Max Instances**.
4. A Prefab with a bottom-centered pivot and geometry roughly **1 Unity unit high** will be scaled by `tree.Size * prefabScale` and placed at the procedural tree terrain position. Different source sizes require adjusting prefabScale.
5. Start a random Magnate city in Play Mode; inspect spawned `Generated <TreeKind> tree (rule)` objects under `City Highways 3D`.

## Safety and LOD
- This is fully optional. No matching, enabled rule component means the original tree meshes/cards are unchanged.
- Manually authored `CityProceduralReplacement` objects run **first** and take precedence. Rules only select remaining trees.
- A rule never replaces a `CardOnly` tree. Those distant trees retain their optimized billboard-only representation.
- Remaining unmatched trees retain the existing chunk mesh/card system and distance switching.
- **Selected replacement Prefabs do not automatically gain tree billboard LOD** in this first implementation. Limit their number (default 8 per rule). Large caps can increase draw calls and memory even when they look good.
- Multiple active rule sets applying to the same city disable automatic selection with a warning rather than applying competing results.
- Selection derives from seed and field position, not session randomness. Rules follow Inspector order, each with an independent Max Instances cap. A zero cap or zero probability disables that entry.
- CityMap, roads, station placement, logical resources, saves and replay data are untouched. Removed tree visuals are omitted only from the 3D presentation plan.

## Acceptance
1. Unity Edit Mode CI passes `CityProceduralTreePrefabRulesTests` and `Magnate3DCompatibilityTests`.
2. With no matching rules, check original tree LOD and far forests in ordinary cities and Magnate seeds 7 and 42.
3. Assign a recognizable tree Prefab, low probability, and Max Instances 1–3. Confirm only the selected types are replaced and never above the configured cap.
4. Verify `CardOnly` far-outskirts trees stay as billboard chunks and close remaining trees still switch between card and 3D based on camera zoom.
5. Disable rules or filter to another city and verify normal geometry returns after restarting the scene.
6. Compare frame time, draw calls and mesh counts in Unity Profiler under low, medium and high quality.