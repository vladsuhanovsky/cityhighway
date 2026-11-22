# Procedural city clearance zones (Stage 1.5)

Author a region of the Unity scene where **procedural buildings and/or trees** should not appear. The intent is to leave space for hand-placed Prefab decorations. This is a visual-generation tool; it does not alter roads, contracts, Core maps, saves, or multiplayer state.

## Usage in Unity 6000.6.8f1

1. Open `Assets/CityHighways/Scenes/CityHighways.unity`.
2. In the Hierarchy select **GameObject → City Highways → Procedural Clearance Zone**.
3. Position its Transform in Scene View, using the X/Z plane. Set `Map Name` to the exact JSON city name (`Москва`, `Чунцин`, `Амстердам`, etc.).
4. Set **Radius** in Unity world units (default 0.5) and choose **Exclude Buildings** and/or **Exclude Trees**. The wire-sphere Gizmo displays the exclusion circle.
5. Place your matching `CitySceneDecoration` anchor with a visual Prefab inside the zone. Save the scene.
6. Start a 3D game for that city. The procedural generator will skip buildings and trees whose placement areas overlap the circle. The hand-placed decoration will still spawn.

Only active, enabled zones matching the current city are used. Multiple zones may overlap. The component affects the generated 3D city only. If no zones are present, procedural generation remains unchanged.

## Safety and limitations

- The exclusion applies **only to buildings and trees**; terrain, decals, river, roads, junctions and smaller procedural props are not suppressed.
- Building placement is tested against the zone with an approximate half-footprint margin, rather than full rotated footprint geometry. Tree placement checks its center.
- This is not a general asset replacement feature: you cannot yet select an individual procedural building and replace its mesh directly. It reserves a space into which you can manually place a visual Prefab.
- Zone shapes are circular in the horizontal X/Z plane. The Gizmo is a 3D wire sphere used as an approximate visual guide.
- Do not place zones over important roads, stations, or gameplay locations expecting to remove or change their rules.

## Checks

- [ ] Edit Mode tests and compilation pass.
- [ ] Compare generated buildings/trees inside and outside a zone in Moscow (3D enabled).
- [ ] Test separate building-only and tree-only zones, and one overlapping pair.
- [ ] Confirm zones for Moscow do not affect Amsterdam or Chongqing.
- [ ] Disable the zone and check the original generation returns.
- [ ] Confirm a placed Prefab still renders, saved games and 2D map remain unchanged.
