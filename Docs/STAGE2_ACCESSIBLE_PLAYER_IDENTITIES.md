# Stage 2 — alternative company colors and road owner markers

Two independent opt-in switches in main-menu Settings → Accessibility (RU/EN). They persist through Display.v1 JSON and reset to off with defaults. Existing saves retain the original presentation. Choose before starting or loading a match/replay; live recoloring of existing 3D meshes is not promised.

- Alternative palette changes the shared Palette.PlayerColor route used by Classic, Magnate, replay and 3D road detail callers; original Palette.Players and resource colors stay unchanged. It is an alternative, not a claim that all color vision deficiencies are covered.
- Stable seat numbers [1]–[5] appear in Classic player HUD, replay player rows, Magnate turn-order list and Classic road tooltips. The turn-order number in Magnate remains separate.
- Map overlay displays a single dark circular badge with one to five white pips and a company-color outline per visible built player-owned road. Pips match seat number. Public/unowned/invalid owners are excluded. It uses built segment geometry in both 2D and 3D. No changes to road markings, meshes, map generation, gameplay rules, replay/save schema or player names.
- Twelve new Edit Mode cases cover seat markers, public owner exclusion, palette, contrast and settings compatibility. Native rendering, density, overlap and readability still require runtime QA.

Manual acceptance: check settings persistence and reset; compare both independent toggles on Classic, co-op, Magnate and replay in RU/EN, flat/3D, desktop and Deck resolution; verify marker position, hidden roads, bridge, partial build, public roads, turn changes and contrast; inspect minimap and trucks (no additional symbols introduced there). Existing 3D materials may need a new match to refresh. No hardware/accessibility certification.
