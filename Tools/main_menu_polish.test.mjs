import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const get = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");
const view = get("Assets/CityHighways/Presentation/MainMenuView.cs");
const road = get("Assets/CityHighways/Presentation/MenuHighwayOverlay.cs");
const route = get("Assets/CityHighways/Presentation/MenuRoadDeparture.cs");
const shade = get("Assets/CityHighways/Presentation/MenuLegibilityVeil.cs");
const widgets = get("Assets/CityHighways/Presentation/MenuWidgets.cs");
const rhythm = get("Assets/CityHighways/Presentation/MenuHomeActionLayout.cs");
const ru = JSON.parse(get("Assets/CityHighways/Resources/CityHighways/Localization/ru.json")).entries;
const en = JSON.parse(get("Assets/CityHighways/Resources/CityHighways/Localization/en.json")).entries;
const home = view.slice(view.indexOf("VisualElement BuildHome(SaveFile saved)"),
  view.indexOf("static Label Pill("));
const bar = view.slice(view.indexOf("VisualElement BuildTopBar()"),
  view.indexOf("void ApplyBackgroundSettings()"));
const corner = view.slice(view.indexOf("VisualElement BuildProfileCorner()"),
  view.indexOf("void RefreshProfileCorner()"));

const translated = (entries, key) => entries.find(e => e.key === key)?.value;

test("golden logo and pixel skyline remain the page's visual anchor", () => {
  assert.match(home, /Ui\.Text\("City Highways", 72f, Theme\.Gold, true\)/);
  assert.match(home, /Fonts\.Weight\.Logo/);
  assert.equal((home.match(/MenuIcon\.Glyph\.Skyline/g) || []).length, 2);
});

test("New Game is always the red highlighted primary tile", () => {
  assert.match(home, /Add\(startGroup, MenuIcon\.Glyph\.Gamepad,\s*LocalizationService\.Get\("menu\.home\.new_game"\),\s*OpenNewGameWithDeparture, true\)/);
  assert.match(widgets, /_primary \? new Color\(0\.75f, 0\.22f, 0\.17f\)/);
});

test("Play Now is a separate action and not a nested New Game badge", () => {
  assert.match(home, /AddSubAction\(startGroup, "menu\.home\.quick_start", StartGame\)/);
  assert.match(home, /group\.Add\(sub\)/);
  assert.doesNotMatch(home, /newGame\.Badge\.Add\(quick\)/);
  assert.equal(translated(ru, "menu.home.quick_start"), "Играть сразу");
  assert.equal(translated(en, "menu.home.quick_start"), "Play Now");
});

test("main column retains exactly the three top-level play intents", () => {
  assert.match(home, /Add\(startGroup, MenuIcon\.Glyph\.Gamepad/);
  assert.match(home, /Add\(learnGroup, MenuIcon\.Glyph\.Cap/);
  assert.match(home, /_onlineButton = Add\(onlineGroup, MenuIcon\.Glyph\.Globe/);
  assert.doesNotMatch(home, /Add\(MenuIcon\.Glyph\.Gear/);
  assert.doesNotMatch(home, /Add\(MenuIcon\.Glyph\.Chart/);
});

test("Rules is adjacent to Tutorial and Clubs is adjacent to Online", () => {
  const tutorial = home.indexOf("MenuIcon.Glyph.Cap");
  const rules = home.indexOf('AddSubAction(learnGroup, "menu.home.rules"');
  const online = home.indexOf("MenuIcon.Glyph.Globe");
  const clubs = home.indexOf('AddSubAction(onlineGroup, "menu.home.clubs"');
  assert.ok(tutorial > 0 && rules > tutorial && online > rules && clubs > online);
});

test("online cloud bans continue showing an explicit notification", () => {
  assert.match(home, /if \(Profiles\.Active\.cloudBanned\)/);
  assert.match(home, /if \(!_bellOpen\) ToggleBell\(\)/);
});

test("profile statistics stays next to the current avatar", () => {
  assert.match(corner, /var corner = Ui\.Column\(\)/);
  assert.match(corner, /_profileCard = new ProfileCard/);
  assert.match(corner, /corner\.Add\(_profileCard\)/);
  assert.match(corner, /Ui\.Button\(LocalizationService\.Get\("menu\.home\.stats"\)/);
  assert.match(corner, /ShowPage\(Page\.Stats\)/);
  assert.doesNotMatch(corner, /"Влад"|"Vlad"/);
  assert.equal(translated(ru, "menu.home.stats"), "Статистика");
});

test("service actions are removed from the main stack", () => {
  assert.doesNotMatch(home, /buttons\.Add\(languageButton\)/);
  assert.doesNotMatch(home, /menu\.home\.settings/);
  assert.match(bar, /TopIcon\(MenuIcon\.Glyph\.Gear, ToggleSettingsPopup\)/);
});

test("gear popover contains both Settings and language with short RU EN labels", () => {
  assert.match(bar, /_settingsPopup = Ui\.Column\(\)/);
  assert.match(bar, /LocalizationService\.Get\("menu\.home\.settings"\)/);
  assert.match(bar, /LocalizationService\.Get\("menu\.home\.language_switch"\)/);
  assert.match(bar, /SwitchMenuLanguage/);
  assert.equal(translated(ru, "menu.home.language_switch"), "Язык: RU → EN");
  assert.equal(translated(en, "menu.home.language_switch"), "Language: EN → RU");
});

test("popover closes on page changes and cannot cover New Game setup", () => {
  assert.match(view, /if \(_settingsPopup != null\)\s*\{\s*_settingsPopupOpen = false;\s*Ui\.Show\(_settingsPopup, false\)/);
  assert.match(view, /ToggleSettingsPopup\(\);\s*ShowPage\(Page\.Settings\)/);
});

test("top icon controls are keyboard and gamepad focusable", () => {
  const top = view.slice(view.indexOf("VisualElement TopIcon("),
    view.indexOf("VisualElement BuildTopBar()"));
  assert.match(top, /button\.focusable = true;/);
  assert.match(top, /RegisterCallback<NavigationSubmitEvent>/);
  assert.match(top, /evt\.StopPropagation\(\)/);
});

test("pixel highway and veil appear after background but behind all controls", () => {
  const background = view.indexOf("Root.Add(_background)");
  const highway = view.indexOf("Root.Add(_highway)");
  const veil = view.indexOf("Root.Add(_legibility)");
  const host = view.indexOf("Root.Add(_pageHost)");
  assert.ok(background > 0 && highway > background && veil > highway && host > veil);
});

test("the new scenic road is visible with both static key art and live city", () => {
  assert.match(view, /Ui\.Show\(_highway, page == Page\.Home\)/);
  assert.match(view, /Ui\.Show\(_legibility, page == Page\.Home\)/);
  assert.doesNotMatch(view, /Ui\.Show\(_highway, !_display\.menuKeyArt\)/);
});

test("decorative overlays never intercept gameplay or menu input", () => {
  assert.match(road, /pickingMode = PickingMode\.Ignore;/);
  assert.match(shade, /pickingMode = PickingMode\.Ignore;/);
});

test("gradient falls off horizontally and before the highway", () => {
  assert.match(shade, /float horizontal = Mathf\.Clamp01\(1f - Mathf\.Abs\(x - 0\.5f\) \/ 0\.38f\)/);
  assert.match(shade, /0\.81f - y/);
  assert.match(shade, /0\.50f \* ease \* vertical/);
});

test("pixel vehicles replace ambiguous headlight dots on the new signature road", () => {
  assert.match(road, /static void PixelCar\(/);
  assert.match(road, /mesh\.Rect\(new Rect\(x - w \* 0\.5f/);
  assert.match(road, /PixelCar\(_mesh, x, cy, paint, right\)/);
  assert.match(road, /new Color\(0\.94f, 0\.38f, 0\.31f\)/);
  assert.match(road, /new Color\(0\.22f, 0\.69f, 0\.76f\)/);
});

test("highway exit has visible asphalt ribbon, lane markings and traffic light", () => {
  assert.match(road, /An explicit off-ramp/);
  assert.match(road, /_mesh\.Quad\(/);
  assert.match(road, /The exit signal turns green only after New Game is pressed/);
  assert.match(road, /new Color\(0\.16f, 0\.98f, 0\.42f\)/);
});

test("green-signal departure uses the highway car's actual turning path", () => {
  assert.match(road, /MenuRoadDeparture\.Position\(DepartureProgress, r\.width, r\.height\)/);
  assert.match(road, /PixelCar\(_mesh, p\.x, p\.y,/);
  assert.match(route, /public const float DurationSeconds = 0\.85f/);
  assert.match(route, /float turn = \(t - 0\.55f\) \/ 0\.45f/);
});

test("New Game controls are gated against repeated activation", () => {
  assert.match(view, /if \(_page != Page\.Home \|\| _newGameTransition >= 0f\) return;/);
  assert.match(view, /_newGameTransition = MenuRoadDeparture\.DurationSeconds;/);
  assert.match(view, /_highway\.BeginDeparture\(\)/);
});

test("reduced motion skips delays and avoids animating unrelated pages", () => {
  assert.match(view, /if \(_display\.reducedMotion\)\s*\{\s*ShowPage\(Page\.NewGame\);\s*return;/);
  assert.match(view, /_highway\.Tick\(dt, _display\.reducedMotion\)/);
  assert.match(view, /if \(page != Page\.Home\) _newGameTransition = -1f/);
  assert.match(road, /if \(!reducedMotion\) _trafficTime \+= dt;/);
});

test("two languages expose the same menu action keys without duplicate entries", () => {
  const keys = ["menu.home.new_game", "menu.home.quick_start", "menu.home.tutorial",
    "menu.home.rules", "menu.home.online", "menu.home.clubs",
    "menu.home.stats", "menu.home.settings", "menu.home.language_switch"];
  for (const lang of [ru, en]) {
    for (const key of keys) {
      assert.equal(lang.filter(e => e.key === key).length, 1, key);
      assert.ok(translated(lang, key).length > 0, key);
    }
  }
});

test("notification bell and settings cannot cover one another", () => {
  assert.match(view, /if \(!_settingsPopupOpen && _bellOpen\) ToggleBell\(\)/);
  assert.match(view, /if \(_bellOpen && _settingsPopupOpen\)/);
  assert.match(view, /Ui\.Show\(_settingsPopup, false\)/);
});

test("Escape and Steam Deck back dismiss a visible settings popover first", () => {
  const tick = view.slice(view.indexOf("public void Tick(float dt)"),
    view.indexOf("// ================================================================== Новая игра"));
  assert.match(tick, /if \(_settingsPopupOpen\) ToggleSettingsPopup\(\);\s*else Ui\.GoBack\(Root\)/);
  assert.match(tick, /keyboard\.escapeKey\.wasPressedThisFrame/);
  assert.match(tick, /pad\.buttonEast\.wasPressedThisFrame/);
});
