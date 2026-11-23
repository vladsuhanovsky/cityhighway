# Clickable procedural object markers in Unity Scene View

This extends **City Highways → 3D Objects → Procedural Object Catalog** with a marker overlay for the **saved** procedural catalog.

## Workflow

1. Open `Assets/CityHighways/Scenes/CityHighways.unity` and the **Procedural Object Catalog** window.
2. Enter Play Mode and launch a 3D city. Click **Capture live 3D catalog for Edit Mode**.
3. Exit Play Mode; the saved catalog is loaded from the local `Library/CityHighwaysProceduralCatalog.json` file.
4. Enable **Show clickable markers in Scene View** in the Catalog window.
5. In Scene View navigate close to the city area you want to edit. The overlay shows up to **120** clickable dots within **2.5 world units** of the Scene View pivot. The list's **Type** and **Find ID** filters also apply to the dots. Blue dots represent buildings; green dots represent trees.
6. Click a dot. The selected ID appears in the Catalog window and next to the selected dot. **Focus** on a list row can move Scene View to a specific object's approximate location.
7. Select an existing **Procedural Object Replacement** anchor in the Hierarchy, then click **Assign selected marker** in the Catalog window. This writes its `Stable Object Id`, `Target` and `Map Name` with Undo support. Save the scene and verify the Prefab replacement in 3D.

## Design boundaries

- Clicks **select an ID in the Catalog window**, not a procedural GameObject in the Hierarchy: procedural buildings and trees are combined into generated meshes and do not exist as separately selectable GameObjects.
- Scene marker positions use the flat X/Z projection of procedural field coordinates; terrain height is not represented. The dots are editor handles, not meshes or colliders.
- Markers appear **only in Edit Mode**, using the saved catalog. Do not modify replacement anchors during Play Mode, because changes would be discarded when Play Mode ends.
- The overlay limits markers near the current Scene View pivot for responsiveness. If you do not see an object, use its **Focus** button or adjust the filter/Scene View navigation.
- Catalog snapshots may become stale after changing map locations, generation density, exclusion zones, graphics quality or generator code. Capture a new 3D catalog after such changes.
- No map JSON, game rules, saves, replay data or runtime procedural generation logic are modified by the overlay.

## Acceptance checklist

- [ ] Unity Edit Mode CI compiles.
- [ ] After capture and Play Mode exit, enable the Scene View overlay and see clickable dots.
- [ ] Verify distinct colors for buildings and trees, and selection label for a clicked dot.
- [ ] Confirm Type/Find ID filters and Focus narrow the visible markers.
- [ ] Select an anchor, click Assign selected marker, save scene and verify its stable ID persists.
- [ ] Verify the overlay produces no runtime GameObjects or gameplay changes.
