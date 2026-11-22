# Wireframe preview of decoration Prefabs in Unity Scene View

1. Open `Assets/CityHighways/Scenes/CityHighways.unity`.
2. Create or select a `CitySceneDecoration` anchor; assign a **Visual Prefab** containing a MeshFilter or SkinnedMeshRenderer.
3. Enable **Preview Wireframe In Scene** on the anchor's Inspector (on by default).
4. Select the anchor and enable **Gizmos** in Scene View. The prefab's meshes appear as an **editor-only wireframe**, transformed by the anchor's position, rotation and scale.
5. Move/rotate/scale the anchor using Unity tools. Press Play and enter a matching map in 3D to verify the actual shaded instance.

## Limitations and safety

- The wireframe is a preview of the prefab's mesh geometry, not a shaded scene render. Materials, terrain, shadows and collisions are not previewed. Prefabs made entirely of UI/SpriteRenderer or other non-mesh renderers show only the anchor guide.
- Geometry comes from the Prefab asset; no additional GameObjects or instances are created, saved or included in builds.
- It draws only for a selected anchor and limits the mesh count to 128 for editor responsiveness.
- **Preview Wireframe In Scene** exists only in the Unity Editor; it has no runtime impact.
- Original map JSON, gameplay rules, save data and procedural city generation are unchanged.

## Verification

- [ ] Scene View displays wireframes for a mesh Prefab and updates position, rotation and scale.
- [ ] Disabling Preview Wireframe In Scene hides the mesh but retains the anchor guide.
- [ ] Deselecting the anchor removes the preview; selected anchor with no assigned Prefab is safe.
- [ ] Play Mode still spawns the Prefab exactly once on its matching 3D map.
- [ ] Unity Edit Mode tests and CI compile cleanly.
