# Stage 3 — ghost raid request and timeline safety

Two verified gaps in the existing ghost feature:
- The new-game banner could launch a replay or raid after the player changed page or record while downloading. One request now owns both watch and raid controls; page/record changes invalidate it. Identical refreshes retain the selected ghost and pending request, while account generation or record metadata changes invalidate it. Stale success/error cannot navigate or enable a newer request.
- A failed replay command previously broke out of timeline simulation and published the partial score array. Invalid author seats, empty history and failed commands now leave the comparison unavailable. Valid recordings retain the existing score calculation. The recorded final target remains visible; the HUD explains unavailable round-by-round comparison. This does not verify the recorded final score or prevent a raid from using valid initial conditions alone.

Eight Edit Mode cases cover duplicate/stale requests, record identity, successful/failed timeline, author seat and empty history. Local missing-replay messages have RU/EN text. No server, schema, rules or Steam SDK changes.

Manual checks: request a cloud replay then navigate away/change city/account; no late launch. Double click watch/raid; only one intent. Retry after failure; buttons recover. Verify ordinary local/cloud raid and unavailable timeline fallback. Live Steam/cloud QA still pending.
