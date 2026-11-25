# Stage 4 — three cooperative difficulty levels

Task #8 from the original roadmap: easy, normal, hard. Balance values are initial design hypotheses, not validated win rates.

| Parameter | Easy | Normal (legacy) | Hard |
| --- | --- | --- | --- |
| Base order rounds | 6 | 4 | 3 |
| Initial stagger | 3 | 2 | 1 |
| Crisis maximum | 15 | 12 | 10 |
| Initial crisis | 0 | 1 | 2 |
| Rounds per passive crisis | 4 | 3 | 2 |
| Penalty per failed order | 1 | 2 | 3 |
| Target (2/3/4/5 players) | 3/4/5/6 | 4/5/6/7 | 5/6/7/8 |

Normal is zero for compatibility with legacy JSON; all concrete rules remain in GameConfig and are not reapplied to old saves or replay. Local and host setup select difficulty; clients receive the authoritative config. Scenario/road-piece settings remain independent. Existing Magnate difficulty unchanged. Network protocol 10 requires all participants to update.

Replay metadata and retention separate local difficulty tiers, and local ghost searches use the same tier. Non-normal co-op results are not uploaded to social record or club contribution APIs until the server supports difficulty-aware scoring; local stats and ordinary profile rewards remain unchanged. Replay UI labels difficulty. The server and production database are untouched.

15 new parameterized Edit Mode cases cover defaults, rules, player count scaling, serialization/replay, retention and labels. Manual QA: new game compact screen/Deck and online host controls RU/EN; save and resume, replay and ghost, actual bot/human games at each level and map; measure win rates/order expiry before tuning. New cities, special events and campaign are separate roadmap work.
