# Stage 4 exit / Stage 5 Workshop preparation

## Stage 4 implementation audit (25 Nov 2026)

- #130: three cooperative presets. Non-normal co-op cloud records are gated; server-side difficulty tiers remain open.
- #131: Supply Season and two optional events; provisional balance, cloud records still grouped by map/mode.
- #132: 12 local campaign missions with isolated progress/checkpoints. Full playthroughs and balance remain open.
- #133: Paris and Berlin, each 42 locations, 79 roads, 48 contracts and 7 goals. Merge aa0da5f.
  Final head efccbba passed Unity Edit Mode #511, Interface Editor #26, Magnate river validation #3 and main menu visual guard #17.
  Visual 2D/3D, all mission/map playthroughs, network, balance and Deck checks are not established by CI.
  
Implementation scope is complete; release acceptance on roadmap #115 is NOT complete.

## Workshop preparation scope

WorkshopMapValidator is an isolated preflight for Classic JSON using existing CityMapDto and CityMap.Validate.
It limits UTF-8 input to 256 KiB, object counts, IDs, labels, coordinates, road resources, basic scores and river/ring size;
requires a full starting contract/goal deck and connected complete and initially open graphs.
Returns a stable diagnostic code and no map on rejection. No file paths, file writes, executable content,
Steam SDK calls, map registry changes or menu entry. Built-in maps retain their existing loading path.
Limits are initial authoring policy, not an agreed gameplay balance standard.
Unity JsonUtility is a permissive deserializer; this is not strict JSON-schema validation or a sandbox.
No bridge intersection, full goal attainability, runtime performance or gameplay balance guarantees.

Next implementation gates: versioned package and safe filesystem import/export with immutable content hash;
editor draft model and undo; preview and local-only game setup; save/replay identity and isolation from
official records, rewards, campaigns and network; only then optional native Workshop upload/download and management.
Native integration requires real registered AppID (not 480), installed Steamworks.NET, configured Steam Cloud quotas
and enabled ISteamUGC in Steamworks Partner. See https://partner.steamgames.com/doc/features/workshop/implementation.
Keep publication disabled until release acceptance. No Steam configuration changed by this PR.

## Tests and acceptance

16 NUnit cases: two known boards, four invalid payloads, UTF-8 size limit and nine invalid map variants.
Run Unity Edit Mode CI on exact HEAD. Later editor integration must test malformed external files,
package identity, adversarial geometry, save/load and local-only isolation. No native Steam or Unity runtime
execution is claimed by this preparation batch.
