# Stage 2 — Replay controller and transport acceptance

ReplayView previously did not call the controller focus maintenance shared by gameplay views. Its start/end seek actions could also leave autoplay active, and transport controls did not reflect replay boundaries or failures.

- Use existing UI Toolkit Submit, controller-intent focus repair and registered Back action; mouse and keyboard remain supported. Escape exits the replay. CityHighwaysApp does not tick a newly opened menu with the same replay Back input.
- Dedicated RU/EN controller help row (not an overlay), stable names on playback controls, native slider and existing device-specific labels. No R3 map-targeting hint: replay map remains read-only.
- Disable unavailable transport actions, preserve restart at the end, allow backward recovery from an execution error, pause autoplay on every seek and reset the playback interval. Do not attempt forward replay after a failed command.
- Localize playback/back/hands labels, including live language changes. This is not full replay/journal localization.
- Nine transport-policy cases and two guide cases. No replay format, game rules, networking or Steam SDK changes.

Manual acceptance: open local and downloaded replay; navigate with mouse/keyboard and Xbox/PS/Deck controller; test B/Escape returns exactly one level, mouse movement and controller reconnect, slider, first/last, pause/restart and speed, error recovery, RU/EN, 1280×800 and desktop. Confirm read-only map and unchanged recorded results. Edit Mode does not verify physical devices or live focus dispatch.
