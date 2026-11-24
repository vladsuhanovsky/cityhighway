# Steamworks Partner achievement API names

Source of truth: `Assets/CityHighways/Presentation/Achievements.cs` → `Achievements.All` (28 IDs as of this PR).

In Steamworks Partner → Achievements, create and publish achievement definitions with **API Name exactly equal** to each of these case-sensitive values. Display names, descriptions, icons and hidden-status settings are localized Steamworks metadata. API names themselves must not be translated.

| Steamworks API Name | In-game category |
| --- | --- |
| `first_contract` | Classic/co-op / cumulative |
| `first_win` | Classic/co-op / cumulative |
| `bridges5` | Classic/co-op / cumulative |
| `tolls15` | Classic/co-op / cumulative |
| `clean4` | Classic/co-op / cumulative |
| `honest_win` | Classic/co-op / cumulative |
| `whistle` | Classic/co-op / cumulative |
| `final_order` | Classic/co-op / cumulative |
| `mayor` | Classic/co-op / cumulative |
| `network8` | Classic/co-op / cumulative |
| `traffic3` | Classic/co-op / cumulative |
| `upgrades3` | Classic/co-op / cumulative |
| `score130` | Classic/co-op / cumulative |
| `big_win` | Classic/co-op / cumulative |
| `coop_win` | Classic/co-op / cumulative |
| `coop_clean` | Classic/co-op / cumulative |
| `mg_first` | Logistics Magnate |
| `mg_win` | Logistics Magnate |
| `mg_nojam` | Logistics Magnate |
| `mg_cold` | Logistics Magnate |
| `mg_hub` | Logistics Magnate |
| `mg_contracts` | Logistics Magnate |
| `mg_legend` | Logistics Magnate |
| `mg_expert` | Logistics Magnate |
| `seasons` | Classic/co-op / cumulative |
| `companies` | Classic/co-op / cumulative |
| `travel` | Classic/co-op / cumulative |
| `veteran` | Classic/co-op / cumulative |

Notes:
- The “classic/co-op / cumulative” category groups both mode-specific and cross-mode lifetime achievements; it is **not** an assertion that every ID can be unlocked in both play modes.
- The game does not auto-create Steamworks metadata. If one ID is missing/unpublished, SteamUserStats.GetAchievement/SetAchievement can fail; the publisher keeps that ID queued for a later retry.
- The current repository uses development `steam_appid.txt` **480**. Set up and publish these in the game's actual production AppID; never claim Steam integration works from the Edit Mode job alone.
- When adding, renaming or removing in-game achievement IDs, update this list and the Partner backend in the same release. Prefer stable API names to avoid breaking previously awarded Steam achievements.
