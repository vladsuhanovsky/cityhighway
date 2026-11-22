# Per-city procedural building and tree density

Unity 6000.6.8f1 — this is an **optional visual override**, not a change to game logic, map data, or saved games.

## How to use

1. Open `Assets/CityHighways/Scenes/CityHighways.unity`.
2. In the Hierarchy choose **GameObject → City Highways → City Generation Tuning**.
3. In Inspector, set `Map Name` to the exact name in your map's JSON (for example `Москва`, `Чунцин`, `Амстердам`).
4. Adjust **Building Density** and **Tree Density** independently. Both values range from 0.5 to 1.5; **1.0** is the original game behavior.
5. Save the Unity scene and test the city with **3D graphics enabled**. To restore defaults, set both controls to 1.0 or remove/disable the component.

## Semantics

- `Building Density` modifies the procedural city-block and outskirt-village building placement probability. It does not change interactive roads, locations, 2D board or contracts.
- `Tree Density` modifies the number of procedural trees in parks, forest zones, riverbanks and outskirts.
- Quality-level density scaling remains active; these Inspector values are **additional multipliers**.
- Procedural decals, props and other decorations keep the previous density behavior.
- No config on the scene, disabled config, different city, or duplicate configs for the same city: **fallback to the previous 1.0 behavior**. Duplicate configs emit a warning instead of picking an arbitrary one.
- This first increment does not replace individual buildings or trees with placed Prefabs; it only exposes visual density parameters.

## Acceptance checklist

- [ ] Unity Editor compiles and Edit Mode CI passes.
- [ ] No tuning object: generated city matches previous layout.
- [ ] Add an object for Moscow, set building density to 0.5 and tree density to 1.5: compare the visual effect in 3D.
- [ ] Change to another city: Moscow tuning is ignored.
- [ ] Disable tuning component: previous density restored.
- [ ] Verify 2D board, saves, replays and gameplay are unchanged.
