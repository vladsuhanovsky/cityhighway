# Procedural prop Prefab rules — lamps, bushes and boats

## Purpose
Optional, seed-independent rules choose visual Prefabs for generated props in ordinary cities and random Logistic Magnate maps. Rules do not modify the logical map, road network, contracts, saves or replay format.

## Authoring
1. In the startup scene select **GameObject → City Highways → Procedural Prop Prefab Rules**.
2. Leave **Map Name** empty to use the same rules for every city, including random Magnate seeds, or enter a fixed CityMap.Name.
3. Add entries to **Rules**, choosing **Prop Kind** (Lamp, Bush, Boat), Visual Prefab, Probability (0–1), Prefab Scale (0.05–5) and Max Instances.
4. The Prefab should be authored at the correct physical dimensions in Unity world units. Its authored local scale is multiplied by Prefab Scale; this system intentionally does not derive scale from prop.Size, since lamp/boat/bush meshes use different sizes.
5. Generated props use their original procedural X/Z, original yaw and terrain-relative height. Boats retain the existing WaterLevel minus 0.001 height convention.

## Compatibility and performance
- Without a matching enabled component, all props stay in the existing batched district meshes. No automatic Prefab instances are added.
- Selection is deterministic using each prop's seed and field position. Rule order defines precedence and Max Instances caps each rule independently.
- Selected Prefabs become children of district-specific `Rule props <district>` roots and participate in the existing `districtOpen` visibility updates. The `_` (non-district) group is always visible.
- Unselected props remain combined into the original district meshes. This keeps batching for the majority of lamps, bushes and boats.
- Multiple active rule components matching a city disable automatic prop rules for that city with a warning.
- **Large instance caps can increase draw calls**; this is primarily an artistic replacement feature. Measure cost in Unity Profiler and the existing 3D Geometry Audit.
- The procedural tree billboard LOD and building rules are unchanged. Rule-created prop Prefabs do not gain a separate LOD automatically.

## Acceptance
1. Verify Unity Edit Mode CI including `CityProceduralPropPrefabRulesTests`.
2. With no rule-set object, compare ordinary and Magnate city appearance to the previous build.
3. Add a Lamp rule with a clear Prefab, Probability 1 and Max Instances 2; confirm exactly two replacements and the rest are still procedurally batched.
4. Test a Bush and Boat rule, check boat waterline and orientation, and toggle district visibility.
5. Generate Magnate maps using seeds 7 and 42; verify rules apply without affecting roads or locations.
6. Disable the rule set or enter another Map Name and restart; original prop meshes should return.
7. Profile CPU/rendering overhead before choosing larger Max Instances.