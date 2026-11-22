# Decoration anchor Inspector preview

After importing the branch into Unity, select a `CitySceneDecoration` anchor in `CityHighways.unity`.

- Assign the **Visual Prefab** in Inspector to show an editor thumbnail and a button to locate the asset.
- **Map Name** must match the loaded map name exactly. **Show In Game** toggles spawning.
- Standard Unity Move / Rotate / Scale tools edit the anchor in Scene View. The green arrow marks its forward direction; the label shows its map name.
- This is **not** a live mesh preview of the prefab in the editor. The real instance appears during 3D gameplay only, then is destroyed when that runtime scene is disposed.
- The procedural 3D buildings and trees are unchanged; this is an additive decoration workflow.

Verification: select an anchor with a prefab, see thumbnail and scene label, change transform, enter Play Mode on matching map and check the instance. Verify no duplicate objects after returning to the menu.
