using System.IO;
using CityHighways.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unified visual authoring surface for the existing procedural UI Toolkit
/// screens. The preview is schematic, not a UXML/UI Builder scene preview.
/// All values are serialized assets and are safe to commit to Git.
/// </summary>
public sealed class InterfaceEditorWindow : EditorWindow
{
    const string ThemePath =
        "Assets/CityHighways/Resources/CityHighways/UI/InterfaceTheme.asset";
    const string MainMenuPath = MainMenuStyleEditorMenu.AssetPath;

    static readonly string[] Tabs =
    {
        "Theme", "Main Menu", "New Game", "Online",
        "Rules", "Settings", "Stats & Profile", "Game HUD"
    };

    static readonly string[][] Properties =
    {
        new[] { "background", "panel", "panelLight", "panelHover",
            "text", "textDim", "accent", "accentHover", "gold", "success",
            "panelGlassOpacity", "commonButtonMinHeight",
            "commonButtonCornerRadius", "cardCornerRadius", "cardSectionSpacing" },
        new[] { "buttonWidth", "primaryHeight", "secondaryHeight",
            "insidePairGap", "betweenGroupsGap", "primaryFontSize",
            "secondaryFontSize", "logoFontSize", "subtitleFontSize",
            "newsWidth", "newsMinHeight", "newsBottomGap",
            "newGameGradientStart", "newGameGradientEnd",
            "secondaryNormal", "secondaryHover", "centerDarkening",
            "trafficSpeed" },
        new[] { "newGameHelpMaxWidth", "newGameOptionsCardWidth" },
        new[] { "onlineNameFieldWidth", "onlineInputMinHeight" },
        new[] { "rulesNavigationWidth", "rulesSearchWidth", "rulesContentHeight" },
        new[] { "settingsNavigationWidth", "settingsPreviewWidth" },
        new[] { "statsHistoryHeight", "statsCharactersWidth",
            "profileContentHeight", "profileSidebarWidth" },
        new[] { "hudTopBarHeight", "hudBottomBarHeight", "hudDrawerWidth",
            "hudPlayersPanelWidth" }
    };

    int _tab;
    Vector2 _scroll;
    InterfaceTheme _theme;
    MainMenuStyle _mainMenu;
    GUIStyle _previewLabel;

    [MenuItem("City Highways/UI/Interface Editor")]
    public static void Open()
    {
        var window = GetWindow<InterfaceEditorWindow>("Interface Editor");
        window.minSize = new Vector2(730f, 440f);
        window.Show();
    }

    void OnEnable() => LoadAssets();

    void LoadAssets()
    {
        _theme = AssetDatabase.LoadAssetAtPath<InterfaceTheme>(ThemePath);
        _mainMenu = AssetDatabase.LoadAssetAtPath<MainMenuStyle>(MainMenuPath);
    }

    static T CreateAsset<T>(string path) where T : ScriptableObject
    {
        string folder = Path.GetDirectoryName(path);
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        var result = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(result, path);
        AssetDatabase.SaveAssets();
        return result;
    }

    void OnGUI()
    {
        if (_theme == null || _mainMenu == null)
        {
            LoadAssets();
            if (_theme == null || _mainMenu == null)
            {
                EditorGUILayout.HelpBox(
                    "One or more interface style assets are missing. " +
                    "Create the missing defaults to continue.", MessageType.Warning);
                if (GUILayout.Button("Create missing style assets"))
                {
                    if (_theme == null) _theme = CreateAsset<InterfaceTheme>(ThemePath);
                    if (_mainMenu == null) _mainMenu = CreateAsset<MainMenuStyle>(MainMenuPath);
                }
                return;
            }
        }

        EditorGUILayout.LabelField("CITY HIGHWAYS / INTERFACE EDITOR",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Edit visual properties of existing screens. Preview is a style " +
            "sketch, not the running game. Reopen the screen / restart Play " +
            "Mode to verify edits. This does not move elements via UXML.",
            MessageType.Info);
        _tab = GUILayout.Toolbar(_tab, Tabs);
        EditorGUILayout.Space(7f);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(335f)))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawProperties();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(6f);
                if (GUILayout.Button("Select style asset in Inspector"))
                {
                    Selection.activeObject = _tab == 1
                        ? (Object)_mainMenu : _theme;
                    EditorGUIUtility.PingObject(Selection.activeObject);
                }
                if (GUILayout.Button("Save style assets"))
                {
                    AssetDatabase.SaveAssets();
                    ShowNotification(new GUIContent("Style assets saved"));
                }
            }
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("SCHEMATIC STYLE PREVIEW",
                    EditorStyles.boldLabel);
                DrawPreview();
                EditorGUILayout.HelpBox(
                    "Only theme colors and the listed dimensions are editable. " +
                    "Open Game View for exact layout, localization and gamepad tests.",
                    MessageType.None);
            }
        }
    }

    void DrawProperties()
    {
        var target = _tab == 1 ? (Object)_mainMenu : _theme;
        var serialized = new SerializedObject(target);
        serialized.Update();

        if (_tab == 0)
            EditorGUILayout.LabelField("Global theme — shared across all screens",
                EditorStyles.boldLabel);
        else
            EditorGUILayout.LabelField(Tabs[_tab] + " / visual dimensions",
                EditorStyles.boldLabel);

        EditorGUILayout.Space(4f);
        EditorGUI.BeginChangeCheck();
        foreach (string name in Properties[_tab])
        {
            var property = serialized.FindProperty(name);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
            else
                EditorGUILayout.HelpBox("Missing style property: " + name,
                    MessageType.Error);
        }
        if (EditorGUI.EndChangeCheck())
        {
            serialized.ApplyModifiedProperties();
            Repaint();
        }

        if (_tab != 0 && _tab != 1)
        {
            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(
                "Shared palette, card and button appearance are controlled " +
                "in the Theme tab. This tab adjusts the selected screen layout.",
                MessageType.Info);
        }
    }

    static void Rect(Rect r, Color color) => EditorGUI.DrawRect(r, color);

    void Text(Rect r, string label, Color color, int size = 12, bool bold = false)
    {
        if (_previewLabel == null) _previewLabel = new GUIStyle(EditorStyles.label);
        _previewLabel.normal.textColor = color;
        _previewLabel.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        _previewLabel.fontSize = size;
        _previewLabel.alignment = TextAnchor.MiddleCenter;
        _previewLabel.clipping = TextClipping.Clip;
        GUI.Label(r, label, _previewLabel);
    }

    void DrawPreview()
    {
        Rect area = GUILayoutUtility.GetRect(270f, 370f, GUILayout.ExpandWidth(true));
        float w = Mathf.Max(250f, area.width);
        float x = area.x + (area.width - w) * 0.5f;
        var canvas = new Rect(x, area.y, w, 370f);
        Rect(canvas, _theme.background);

        float panelWidth = Mathf.Min(w - 28f, 380f);
        float px = x + (w - panelWidth) * 0.5f;
        var panel = new Rect(px, area.y + 28f, panelWidth, 310f);
        Color panelColor = _theme.panel;
        panelColor.a = Mathf.Clamp01(_theme.panelGlassOpacity);
        Rect(panel, panelColor);

        string title = _tab == 1 ? "CITY HIGHWAYS" :
            _tab == 2 ? "NEW GAME" : _tab == 3 ? "ONLINE" :
            _tab == 4 ? "RULES" : _tab == 5 ? "SETTINGS" :
            _tab == 6 ? "STATISTICS / PROFILE" :
            _tab == 7 ? "GAME HUD" : "DESIGN SYSTEM";
        Text(new Rect(panel.x + 8f, panel.y + 12f, panel.width - 16f, 34f),
            title, _theme.gold, 17, true);

        if (_tab == 1)
        {
            float scale = Mathf.Min(0.62f, (panel.width - 30f) /
                Mathf.Max(1f, _mainMenu.buttonWidth));
            float bw = _mainMenu.buttonWidth * scale;
            float h1 = _mainMenu.primaryHeight * scale;
            float h2 = _mainMenu.secondaryHeight * scale;
            float gap1 = _mainMenu.insidePairGap * scale;
            float gap2 = _mainMenu.betweenGroupsGap * scale;
            float by = panel.y + 56f;
            float bx = panel.x + (panel.width - bw) * 0.5f;
            string[] primary = { "NEW GAME", "TUTORIAL", "ONLINE" };
            string[] secondary = { "PLAY NOW", "RULES", "FRIENDS" };
            for (int i = 0; i < 3; i++)
            {
                var main = new Rect(bx, by, bw, h1);
                Rect(main, i == 0 ? _mainMenu.newGameGradientStart : _theme.panelHover);
                Text(main, primary[i], _theme.text, 12, true);
                by += h1 + gap1;
                var sub = new Rect(bx, by, bw, h2);
                Rect(sub, _mainMenu.secondaryNormal);
                Text(sub, secondary[i], _theme.text, 11);
                by += h2 + (i == 2 ? 0f : gap2);
            }
        }
        else if (_tab == 7)
        {
            var hud = new Rect(panel.x + 12f, panel.y + 60f, panel.width - 24f, 217f);
            Rect(hud, _theme.panelLight);
            var top = new Rect(hud.x, hud.y, hud.width,
                Mathf.Clamp(_theme.hudTopBarHeight * 0.65f, 25f, 70f));
            Rect(top, _theme.panelHover);
            Text(top, "TURN / POINTS / ACTIONS", _theme.text, 11);
            var drawer = new Rect(hud.x, top.yMax + 5f,
                Mathf.Min(hud.width * 0.8f, _theme.hudDrawerWidth * 0.5f),
                Mathf.Max(30f, hud.height - top.height - 55f));
            Rect(drawer, _theme.panel);
            Text(drawer, "RESOURCE CARDS", _theme.text, 11);
            var bottom = new Rect(hud.x, hud.yMax -
                Mathf.Clamp(_theme.hudBottomBarHeight * 0.5f, 25f, 55f),
                hud.width, Mathf.Clamp(_theme.hudBottomBarHeight * 0.5f, 25f, 55f));
            Rect(bottom, _theme.panelHover);
            Text(bottom, "BUILD ROAD / END TURN", _theme.text, 10);
        }
        else
        {
            // Only a schematic, but changing dimensions in each screen's
            // editor tab visibly changes the corresponding preview element.
            float nav = _tab == 2 ? _theme.newGameOptionsCardWidth :
                _tab == 3 ? _theme.onlineNameFieldWidth :
                _tab == 4 ? _theme.rulesNavigationWidth :
                _tab == 5 ? _theme.settingsNavigationWidth :
                _tab == 6 ? _theme.profileSidebarWidth : 210f;
            float navWidth = Mathf.Clamp(nav * 0.34f, 65f, panel.width * 0.48f);
            float h = _tab == 4 ? _theme.rulesContentHeight :
                _tab == 6 ? _theme.statsHistoryHeight :
                _tab == 2 ? _theme.newGameHelpMaxWidth :
                _tab == 5 ? _theme.settingsPreviewWidth : 580f;
            float viewportHeight = Mathf.Clamp(h * 0.32f, 115f, 220f);
            var navBox = new Rect(panel.x + 12f, panel.y + 60f,
                navWidth, viewportHeight);
            Rect(navBox, _theme.panelHover);
            string navLabel = _tab == 2 ? "OPTIONS" :
                _tab == 3 ? "PLAYER INPUT" : _tab == 6 ? "PROFILE" : "NAV";
            Text(new Rect(navBox.x, navBox.y + 10f, navBox.width, 32f),
                navLabel, _theme.text, 11, true);
            var content = new Rect(navBox.xMax + 8f, navBox.y,
                Mathf.Max(25f, panel.xMax - navBox.xMax - 20f),
                viewportHeight);
            Rect(content, _theme.panelLight);
            Text(new Rect(content.x + 2f, content.y + 10f,
                content.width - 4f, 36f), "CONTENT", _theme.text, 12, true);

            if (_tab == 3)
            {
                float inputHeight = Mathf.Clamp(_theme.onlineInputMinHeight * 0.9f,
                    20f, 56f);
                Rect(new Rect(navBox.x + 6f, navBox.y + 50f,
                    navBox.width - 12f, inputHeight), _theme.panelLight);
            }
            if (_tab == 4)
            {
                float searchWidth = Mathf.Min(content.width - 12f,
                    _theme.rulesSearchWidth * 0.2f);
                Rect(new Rect(content.x + 6f, content.y + 50f,
                    Mathf.Max(12f, searchWidth), 16f), _theme.panel);
            }
            if (_tab == 5)
            {
                float previewWidth = Mathf.Min(content.width - 12f,
                    _theme.settingsPreviewWidth * 0.19f);
                Rect(new Rect(content.x + 6f, content.y + 50f,
                    Mathf.Max(12f, previewWidth), 45f), _theme.panel);
            }
            if (_tab == 6)
            {
                float chartWidth = Mathf.Min(content.width - 12f,
                    _theme.statsCharactersWidth * 0.19f);
                Rect(new Rect(content.x + 6f, content.y + 50f,
                    Mathf.Max(12f, chartWidth), 36f), _theme.panel);
            }
            var action = new Rect(content.x + 10f, content.yMax - 55f,
                Mathf.Max(15f, content.width - 20f),
                Mathf.Clamp(_theme.commonButtonMinHeight * 0.9f, 22f, 52f));
            Rect(action, _theme.accent);
            Text(action, "ACTION", Color.white, 11, true);
        }

        Rect(new Rect(x, canvas.yMax - 4f, w, 4f), _theme.gold);
    }
}
