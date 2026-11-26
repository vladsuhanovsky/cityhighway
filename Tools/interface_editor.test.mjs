import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const get = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");
const names = {
  theme: "Assets/CityHighways/Presentation/InterfaceTheme.cs",
  themeMeta: "Assets/CityHighways/Presentation/InterfaceTheme.cs.meta",
  themeAsset: "Assets/CityHighways/Resources/CityHighways/UI/InterfaceTheme.asset",
  layout: "Assets/CityHighways/Presentation/InterfaceLayout.cs",
  editor: "Assets/CityHighways/Editor/InterfaceEditorWindow.cs",
  main: "Assets/CityHighways/Presentation/MainMenuView.cs",
  ui: "Assets/CityHighways/Presentation/Ui.cs",
  online: "Assets/CityHighways/Presentation/MenuOnlinePage.cs",
  rules: "Assets/CityHighways/Presentation/MenuRulesPage.cs",
  settings: "Assets/CityHighways/Presentation/MenuSettingsPage.cs",
  stats: "Assets/CityHighways/Presentation/MenuStatsPage.cs",
  profile: "Assets/CityHighways/Presentation/MenuProfilePage.cs",
  hud: "Assets/CityHighways/Presentation/GameView.cs",
  tests: "Assets/CityHighways/Tests/InterfaceThemeTests.cs"
};
const s = Object.fromEntries(Object.entries(names).map(([key, path]) => [key, get(path)]));

test("Interface Editor is available as a real Unity EditorWindow", () => {
  assert.match(s.editor, /public sealed class InterfaceEditorWindow : EditorWindow/);
  assert.match(s.editor, /\[MenuItem\("City Highways\/UI\/Interface Editor"\)\]/);
  assert.match(s.editor, /GetWindow<InterfaceEditorWindow>/);
  assert.match(s.editor, /EditorGUILayout\.BeginScrollView/);
});

test("editor offers all eight meaningful style sections", () => {
  for (const section of ["Theme", "Main Menu", "New Game", "Online",
      "Rules", "Settings", "Stats & Profile", "Game HUD"]) {
    assert.ok(s.editor.includes('"' + section + '"'), section);
  }
  assert.match(s.editor, /GUILayout\.Toolbar\(_tab, Tabs\)/);
});

test("edits are serialized through Unity SerializedObject with undo support", () => {
  assert.match(s.editor, /new SerializedObject\(target\)/);
  assert.match(s.editor, /serialized\.FindProperty\(name\)/);
  assert.match(s.editor, /EditorGUILayout\.PropertyField\(property, true\)/);
  assert.match(s.editor, /serialized\.ApplyModifiedProperties\(\)/);
  assert.match(s.editor, /AssetDatabase\.SaveAssets\(\)/);
});

test("editor reuses shipped MainMenuStyle, never creates conflicting menu assets", () => {
  assert.match(s.editor, /MainMenuStyleEditorMenu\.AssetPath/);
  assert.match(s.editor, /_tab == 1 \? \(Object\)_mainMenu : _theme/);
  assert.match(s.editor, /Selection\.activeObject = _tab == 1/);
});

test("editor has schematic visual preview rather than claiming exact game rendering", () => {
  assert.match(s.editor, /SCHEMATIC STYLE PREVIEW/);
  assert.match(s.editor, /EditorGUI\.DrawRect/);
  assert.match(s.editor, /not a UXML\/UI Builder scene preview/);
  assert.match(s.editor, /_theme\.panel/);
  assert.match(s.editor, /_mainMenu\.buttonWidth/);
});

test("global theme Resources asset exists and its script reference GUID matches", () => {
  assert.match(s.theme, /\[CreateAssetMenu\(/);
  assert.match(s.theme, /Resources\.Load<InterfaceTheme>\(ResourcePath\)/);
  assert.match(s.theme, /CityHighways\/UI\/InterfaceTheme/);
  const guid = s.themeMeta.match(/guid: ([0-9a-f]{32})/)?.[1];
  assert.ok(guid, "missing C# meta GUID");
  assert.ok(s.themeAsset.includes("guid: " + guid + ", type: 3"));
});

test("every editable theme field is persisted in the real YAML asset", () => {
  const fields = [...s.theme.matchAll(/public (?:float|Color) (\w+) =/g)].map(x => x[1]);
  assert.ok(fields.length >= 30, "the theme should cover multiple screens");
  for (const name of fields) {
    assert.match(s.themeAsset, new RegExp("^  " + name + ":", "m"), name);
    assert.match(s.editor, new RegExp("\\b" + name + "\\b"), name);
  }
});

test("theme drives shared palette of menus and HUD", () => {
  assert.match(s.ui, /static InterfaceTheme Style => InterfaceTheme\.Current/);
  for (const [property, field] of [["Panel", "panel"], ["Text", "text"],
      ["Accent", "accent"], ["Gold", "gold"], ["Success", "success"],
      ["PanelLight", "panelLight"], ["TextDim", "textDim"]]) {
    assert.match(s.ui, new RegExp("public static Color " + property +
      " => Style != null \\? Style\\." + field));
  }
});

test("existing palette defaults remain available without the theme asset", () => {
  assert.match(s.ui, /Style\.panel : Hex\(0x2C3E50\)/);
  assert.match(s.ui, /Style\.text : Hex\(0xECF0F1\)/);
  assert.match(s.ui, /Style\.accent : Hex\(0xE74C3C\)/);
});

test("shared UI helper uses configurable buttons, cards and section gap", () => {
  assert.match(s.ui, /button\.style\.minHeight = InterfaceLayout\.CommonButtonHeight/);
  assert.match(s.ui, /Radius\(button, InterfaceLayout\.CommonButtonRadius\)/);
  assert.match(s.ui, /Radius\(e, InterfaceLayout\.CardRadius\)/);
  assert.match(s.ui, /label\.style\.marginBottom = InterfaceLayout\.SectionGap/);
});

test("layout clamps edited sizes and rejects NaN/Infinity", () => {
  assert.match(s.layout, /public static float Resolve\(float\? value, float fallback, float min, float max\)/);
  assert.match(s.layout, /float\.IsNaN\(value\.Value\)/);
  assert.match(s.layout, /float\.IsInfinity\(value\.Value\)/);
  assert.match(s.tests, /RangeLimiterRejectsNonFiniteValues/);
});

test("New Game screen responds to card and help widths", () => {
  assert.match(s.main, /characterHint\.style\.maxWidth = InterfaceLayout\.NewGameHelpMaxWidth/);
  assert.match(s.main, /gameCard\.style\.width = InterfaceLayout\.NewGameOptionsCardWidth/);
});

test("Online screen responds to input width and input height", () => {
  assert.match(s.online, /_nameField\.style\.width = InterfaceLayout\.OnlineNameFieldWidth/);
  assert.match(s.online, /input\.style\.minHeight = InterfaceLayout\.OnlineInputMinHeight/);
});

test("Rules screen responds to sidebar, search and reader viewport sizes", () => {
  assert.match(s.rules, /_nav\.style\.width = InterfaceLayout\.RulesNavigationWidth/);
  assert.match(s.rules, /searchBox\.style\.width = InterfaceLayout\.RulesSearchWidth/);
  assert.match(s.rules, /_content\.style\.height = InterfaceLayout\.RulesContentHeight/);
});

test("Settings screen responds to sidebar and preview width", () => {
  assert.match(s.settings, /_nav\.style\.width = InterfaceLayout\.SettingsNavigationWidth/);
  assert.match(s.settings, /previewCard\.style\.width = InterfaceLayout\.SettingsPreviewWidth/);
});

test("statistics and profile screens use their own layout controls", () => {
  assert.match(s.stats, /scroll\.style\.height = InterfaceLayout\.StatsHistoryHeight/);
  assert.match(s.stats, /characters\.style\.width = InterfaceLayout\.StatsCharactersWidth/);
  assert.match(s.profile, /ContentHeight => InterfaceLayout\.ProfileContentHeight/);
  assert.match(s.profile, /left\.style\.width = InterfaceLayout\.ProfileSidebarWidth/);
});

test("classic game HUD uses adjustable bars, drawer and player panel", () => {
  assert.match(s.hud, /TopBarHeight => InterfaceLayout\.HudTopBarHeight/);
  assert.match(s.hud, /BottomBarHeight => InterfaceLayout\.HudBottomBarHeight/);
  assert.match(s.hud, /DrawerWidth => InterfaceLayout\.HudDrawerWidth/);
  assert.match(s.hud, /PlayersPanelWidth => InterfaceLayout\.HudPlayersPanelWidth/);
});

test("theme editing never replaces localized menu actions or their callbacks", () => {
  assert.match(s.main, /OpenNewGameWithDeparture/);
  assert.match(s.main, /AddSubAction\(startGroup, "menu\.home\.quick_start", StartGame\)/);
  assert.match(s.main, /AddSubAction\(learnGroup, "menu\.home\.rules"/);
  assert.match(s.main, /AddSubAction\(onlineGroup, "menu\.home\.clubs"/);
});

test("menu editor remains independent from gameplay rules and server deployments", () => {
  assert.doesNotMatch(s.theme, /CityHighways\.Core|Supabase|NetSession|Steam/);
  assert.doesNotMatch(s.editor, /NetSession|GameCommand|Supabase|HTTP/);
});

test("screens do not need custom inspector scripts to take theme edits", () => {
  for (const key of ["main", "online", "rules", "settings", "stats", "profile", "hud"])
    assert.ok(s[key].includes("InterfaceLayout."), key);
});


test("profile has five primary routes and retains wallet, style, account management and canonical statistics", () => {
  const tabs = [...s.profile.matchAll(/AddTab\(tabs, Tab\.(\w+),/g)].map(m => m[1]);
  assert.deepEqual(tabs, ["Overview", "Wardrobe", "Collection", "Achievements", "Mastery"]);
  assert.match(s.profile, /_walletButton = Btn\("", \(\) => Navigate\(Tab\.Wallet\)/);
  assert.match(s.profile, /case Tab\.Wallet: AddOverviewReturn\(\); WalletTab\(profile\)/);
  assert.match(s.profile, /case Tab\.Style: AddOverviewReturn\(\); StyleTab\(profile\)/);
  assert.match(s.profile, /case Tab\.Collection: ContainersTab\(profile\)/);
  assert.match(s.profile, /ProfilesCard\(profile\);/);
  assert.match(s.profile, /"profile\.v2\.full_stats"\), _showStats/);
  assert.doesNotMatch(s.profile, /void StatsTab\(|StatsLog\.LoadAll\(/);
  assert.match(s.profile, /_lastResult = null;/);
});
