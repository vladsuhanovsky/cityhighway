# Profile 2.0 + Living City

Source: [approved scope #145](https://github.com/vladsuhanovsky/cityhighway/issues/145), 2026-11-26. Implementation and acceptance are distinct.

## Approved direction — 26 Nov 2026

The user approved the Profile 2.0 visual concepts (dark navy/graphite, orange accents, stylized transport-industry characters, item-focused wardrobe cards and collectible diorama) and the Living City extension. This is the next product priority **after PR #144 is verified and merged**. Temporarily defer further configurable City Generator Lab layouts, then resume the remaining roadmap #115 / Docs/ROADMAP_2_0.md. Do not mark Stage 7 or earlier manual acceptance gates complete. Keep this issue as the approved scope and track progress here.

### Product goal

Make profile customization visible during actual matches, not merely in menus. A player earns influence/experience, unlocks cosmetic items, customizes a character and company/fleet, and sees those choices reflected in the city. No pay-to-win, gameplay bonuses, changed road ownership rules or new monetization assumptions.

### Work packages

| # | Package | Priority |
|---|---|---|
| 1 | Profile navigation refactor; remove duplicate Stats tab; preserve canonical main-menu statistics and add a link to it; wallet balance in header and retain existing wallet functions | P0 |
| 2 | Profile overview: character preview, name, level/title, real XP progress and brief metrics, featured unlocked achievements; use existing data, no invented progress | P0 |
| 3 | Character 2.0: cohesive stylized male/female characters, distinct silhouettes, modular clothing, compatible rig, lighting, camera, idle/rotation preview; audit existing assets and licenses before choosing replacements | P0 |
| 4 | Wardrobe 2.0: uniform item cards with item-only images, rarity and ownership, try-on without purchase, filters, saved outfits and sets; compatible shared unlocks where supported, no loss of existing entitlements | P0 |
| 5 | Company identity: bounded name, predefined emblem and primary/secondary cosmetic colors, separate from accessible gameplay owner colors | P1 |
| 6 | Garage under Collection: vehicle model/livery, emblem, optional cosmetic details; asphalt paver, concrete mixer, grader and crane appropriate to road/bridge type; use existing rewards/economy initially | P1 |
| 7 | Living City construction visuals: show actual builder's personalized machine on newly built roads; short nonblocking animation, fallback model, skip/reduced-motion option; completed road remains authoritative | P1 |
| 8 | Living City player characters: optional visible owner avatars walking along safe pedestrian routes, visiting construction/hubs; select to inspect owner and current outfit; no mandatory avatar control | P1 |
| 9 | Observation camera: focus/follow avatar or vehicle, return to strategic camera and restore input/focus; no obstruction of game controls | P1 |
| 10 | Collections and showcases: existing containers, vehicle miniatures, city sets, medals, selected unlocked achievements and display stands; avoid duplicating achievement progression | P2 |
| 11 | Additional city life: idle behavior, visit/interact and celebration animations; only after MVP meets measured budgets | P2 |
| 12 | Photo mode: hide HUD, inspect scene, restore camera/UI; later optional work | P2 |
| 13 | Interface Editor integration, RU/EN localization, accessibility, regression coverage and performance profiling throughout | P0 ongoing |

### Architecture and constraints

- Five primary tabs: Overview, Wardrobe, Collection, Achievements, Mastery. Existing Style features move to relevant sections; existing wallet functions must remain reachable. Detailed match history/analytics stay in canonical Statistics screen; profile only shows concise highlights.
- First inspect current profile models, catalog IDs, unlock/purchase logic, save/profile/cloud boundaries, preview render pipeline, Interface Editor, existing match FX and city traffic/vehicle rendering. Reuse existing systems rather than create parallel progression or event buses.
- Separate cosmetic presentation from deterministic Core: consume existing committed action/FX notifications where possible; no changes to game RNG, scores, turn order, ownership, road connectivity, contracts or authoritative state. Cosmetic snapshots are validated and bounded, with safe default for old profiles, unknown/missing assets and offline players. Do not transmit untrusted assets or arbitrary paths. No live Steam/Supabase changes without separate approval. Defer remote cosmetic synchronization until protocol/identity compatibility and privacy are tested.
- Apply cosmetic assets to the actual action owner (not simply the current local profile); local/hotseat/bot/remote fallback behavior explicit. Ensure replay/load/undo/redo, repeated callbacks and game teardown do not create stale/duplicate effects. Observation and visual simulation cannot block gameplay or consume game RNG.
- Bounded pools, LOD, quality settings, reduced motion, no unnecessary physics/pathfinding for decorative agents; measure cold/warm startup, frame P95/P99, memory and object counts against same-map baseline. No unsupported FPS claims.
- The approved mockups are art direction, not actual Unity assets. New rigged models, item renders, animation clips and commercially usable licenses require asset work/validation. Do not represent 2D generated reference art as finished rigged models.
- Retain current profile data, purchases, inventory, achievement progress, user preferences, saves/replays and profile isolation. No removal of existing features without a replacement path. No real-money systems or economy rebalancing in this scope.

### Acceptance gates

- [ ] Existing profile opens with old inventory and progression intact; no duplicate Stats page, canonical stats link works; all existing wallet/Style/containers features reachable.
- [ ] Five-tab overview/wardrobe UX with genuine data, equal-sized item cards, unowned try-on without charging, valid equip/purchase and persistence; mouse/keyboard/controller, RU/EN, 1280x800 and desktop; Interface Editor support.
- [ ] Actual rigged character and vehicle assets inspected for visual coherence, attachment fit, animation and licensed use.
- [ ] MVP vertical slice: choose a valid cosmetic fleet/character item in profile, start a local match, build a road and see correct owner's vehicle; inspect own avatar in city and return to strategy view. Test both Classic and Magnate where applicable, fallback for unsupported mode/2D/low quality.
- [ ] No changes to deterministic outcomes, save/replay compatibility or action semantics; no duplicate visual effect after undo, replay, reload or view disposal; stable owner markers and accessibility.
- [ ] Profile/visual unit and Play Mode tests, applicable CI green on final HEAD, manual runtime visuals/controller acceptance and measured performance on target hardware. Remote multiplayer/Steam/cloud acceptance is separate, not implied by local MVP.

### Delivery agreement

Manual development only: user reports test completion; verify exact final HEAD for all applicable CI, merge only when green, one Draft PR at a time, separate commits for coherent work. No automatic continuation. First package after #144: audit existing profile paths and data, implement navigation/Stats deduplication and wallet header while preserving current features. Larger new asset work follows separately.

## Batch 1 — implementation audit and scope (CI pending)

- `MenuProfilePage` was a single seven-tab view with partials for containers and mastery. The old StatsTab mixed a local name-based match history with account management. The latter is retained unchanged in ProfilesCard, now in Overview. Detailed statistics continue to use MenuStatsPage / StatsLog, with explicit origin-aware return via MainMenuView. Rebuilding the canonical statistics page on entry uses current data and its existing player/mode selector behavior.
- Overview, Wardrobe, Collection, Achievements, Mastery are five primary routes. Wallet is opened from a header button; Style from Overview. Both show a return to Overview and keep its primary selection. Collection uses the existing containers/collection content. Existing style reward links still reach Mastery and Collection. Primary tabs now use native focusable buttons with equal flex basis.
- Overview uses the existing TurntableCharacter with equipped Outfit, existing ProfileLevel XP/level/rank, profile games/wins and actual unlocked achievements (first four in catalog order, not yet user-pinned). No duplicate recent match history, no fabricated best-score or specialization figures. Full details available through the canonical statistics screen. Existing avatar/name/profile controls remain accessible.
- Existing PlayerProfile and ProfileBook data, owned IDs, Outfit/outfitAlt, wallet history, containers, achievement/mastery fields, cloud and game state are unchanged. Switching active profile clears transient preview/roulette results rather than exposing another player's result. Existing profile content/sidebar InterfaceLayout settings remain in use. New labels have matching RU/EN catalog keys; preexisting localization/asset issues are not declared resolved.
- Existing profile character preview is not a new rigged 3D model. No garage, company data, new economy, in-game avatars/vehicles, remote cosmetic sync or new save schema in this batch. Next: verify actual navigation/account flows, polish Overview and audit character assets/wardrobe, then implement further packages in approved order. Runtime visuals and physical controller/Deck acceptance remain open.

## Manual acceptance for batch 1

Open profile with a populated existing inventory, switch among five sections, open wallet from header and return, open Style from Overview and exercise existing reward routes, open Collection and leave during container animation, switch profiles and verify no old preview/result is shown, confirm avatar/name/create/select/delete controls still available. Open statistics from home and profile and verify Back returns to the respective origin; exercise player/mode selection and refresh. Check RU/EN and compact/desktop viewport, keyboard and controller focus, actual equipped character, wallet refresh after purchases, and Interface Editor profile dimensions. No live service required for local acceptance.
