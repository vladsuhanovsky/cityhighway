# Stage 6 final acceptance audit

Evidence: PR #139 final f50588e, Edit Mode run 45710358095 and Play Mode run 45710358061 succeeded; merged as 9c366e4. Test Results check is neutral (reporting), not a failed Unity workflow.

## Remaining acceptance matrix (open)

| Gate | Procedure | Evidence required |
| --- | --- | --- |
| Standalone boot | Build Windows using CityHighwaysBuildTools; launch on an isolated OS account or VM with no existing saves and offline services | Build log, executable revision, menu screenshot |
| Session lifecycle | Start Classic, return to menu, start Magnate, return; repeat with co-op and campaign | Screen recording and player log; no unexpected exceptions |
| Localization | RU/EN record raid loading, five Classic maps, Magnate, campaign, replay, settings and results at 1280x800 and 1920x1080 | Screenshots with revision and language |
| Performance | Same hardware, seed, camera path, resolution, quality, VSync and cap; at least three cold and three warm runs per revision | Startup durations, frame P95/P99, memory and GC allocation captures |
| HUD | Compare timer idle, road preview and actions with existing FullRefresh/ClockRefresh/MapRefresh markers | Profiler captures, counts and allocations |
| Geometry | Inspect adjusted crosswalks and river/hub clearance on baseline seeds | Lab reports and actual 2D/3D screenshots |
| Hardware | Actual controller and Deck 1280x800 | Input and layout recording |

Do not use a player's real profile or live Steam/Supabase for isolated smoke. Do not infer standalone success from attached UIDocument tests. Existing performance repeat comparisons are advisory; no new measurements are available. The record raid loading title was confirmed by inspecting the caller and LoadingScreen. Other scanner candidates remain heuristic, not confirmed defects. Stage 7 engineering may proceed while manual gates remain tracked; release acceptance stays open.
