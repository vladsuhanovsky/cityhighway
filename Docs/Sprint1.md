# Sprint 1 foundation

## Scope
- GitHub Actions Edit Mode test workflow, pinned to Unity 6000.6.8f1.
- Additional JSON-roundtrip replay determinism test.
- Deterministic generated legacy-version smoke fixtures and their limitations.
- A first LocalizationService with resource JSON catalogs (ru, en).
- Home menu entry points and subtitle localized; other menus, news body, stats and gameplay remain Russian.
- Stable resource/player visual identities and pattern identifiers (rendering unchanged).

## Localization usage
`LocalizationService.Get("menu.home.new_game")` returns the active-language
string. Russian is the default for compatibility while other pages remain untranslated.
The home page has a Russian/English toggle. The menu is rebuilt when switched.
Add a key to both `Resources/CityHighways/Localization/*.json` files before using it.
This is a first migration slice, not complete localization or CJK font support.

## CI setup
Provide Unity activation secrets required by GameCI (typically `UNITY_LICENSE`,
`UNITY_EMAIL`, `UNITY_PASSWORD`) under repository Actions secrets. Run the
workflow and inspect its XML/test artifacts. GitHub-hosted runner cannot run
without an appropriate Unity license/activation method.

## Verification
Source-level review is not a substitute for compiling with Unity Editor.
No Unity build or Edit Mode test execution is claimed until the Actions run succeeds.
