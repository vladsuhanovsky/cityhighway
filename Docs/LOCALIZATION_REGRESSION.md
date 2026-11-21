# City Highways — RU/EN regression checklist

Unity version: **6000.6.8f1** (`ProjectSettings/ProjectVersion.txt`).
Automated EditMode tests: `Assets/CityHighways/Tests`, especially `RulesTests`,
`ScenariosTests`, `DealsUpgradesEventsTests`, `CommandsTests`.
GitHub Actions: `.github/workflows/unity-editmode.yml`.
Localization audit: `python3 Tools/localization_audit.py`.

## Automated verification

1. Open the project in Unity 6000.6.8f1.
2. Open **Window > General > Test Runner**, select **EditMode**, and run all tests.
3. Verify `RulesTests.LiveBuildAndSave_KeepCoreStateIndependentOfLanguage`
   and `RulesTests.LivePendingTrade_SurvivesLanguageChangeSaveLoadAndAcceptance`.
4. Verify the four scenario announcement tests and all existing gameplay
   and command replay tests. Check the CI run on the commit before release.
5. Run `python3 Tools/localization_audit.py`; validation errors must be zero.
   The list of Cyrillic UI candidates is heuristic and must be reviewed manually.

## Manual UI smoke matrix

Perform a new game in **RU** and **EN**. Change language while the game is running
and compare current UI with the journal. User-defined player names can remain
Cyrillic and are not untranslated UI.

| Flow | Steps | Expected result |
| --- | --- | --- |
| Setup | Open a new game and change language | Modes, controls, contracts, scenario descriptions refresh |
| Scenarios | Start Winter, Greater Moscow, Repair Year, Elections | The scenario announcement and game settings use the selected language |
| Construction | Build a road, attempt an occupied or closed road | Journal, tooltips and failure flash translate; game rules unchanged |
| Cards/contracts | Draw resource cards, select contracts, use a luck card | Journal entries and dynamic values translate |
| Trade | Propose a trade with resource cards, influence and road pass; accept and decline | Offers, responses and pass messages translate; quantities stay intact |
| Auctions/bribes | Bid, accuse a player (successful/failed) | Auction and accusation log entries translate without changing player names |
| Traffic | Complete a contract with a rival road; trigger a jam and upgrade | Toll, traffic jam and highway messages translate |
| Influence | Spend influence, claim a City Hall goal | Influence journal reason and amount translate |
| Coop | Complete and miss an order; trigger crisis | Orders, crisis messages and victory/defeat text translate |
| Finale | Trigger final round and final City Hall order | Warning, final order, audit and final scoring remain coherent in EN |
| Switch while playing | Keep a temporary error visible, switch RU → EN → RU | Existing flash and journal entries re-render in chosen language |
| Save/load | Save with pending trade or constructed road, switch language, reload | Identical game state and legal next action |
| Unknown text | Display unsupported legacy journal entry | Preserve original text, do not fabricate a translation |

## Release acceptance

- Unity editor compiles successfully.
- EditMode test run is **green** (not just a successful audit step).
- Zero catalog validation errors and no confirmed untranslated non-user-facing UI.
- Save/load and the same commands behave equivalently under RU and EN.
- Record the CI run URL and any remaining exceptions before release.

**Known limitation:** a test-only GitHub Actions run cannot establish visual
correctness, text clipping or modal refresh behavior. Those require a manual Unity
smoke run in both languages.
