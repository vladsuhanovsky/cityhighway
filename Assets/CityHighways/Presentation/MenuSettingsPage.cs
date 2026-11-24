using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Настройки»: слева — категории с крупными значками (Экран, Звук, Геймплей, Доступность)
    /// и бегущим индикатором, в центре — переключатели, слайдеры и сегменты, справа — живой предпросмотр
    /// (наклон поля, мини-карта, палитра и свет меню, анимации, масштаб). Сверху — быстрые пресеты,
    /// внизу — сброс к заводским. Всё применяется сразу и сохраняется в DisplaySettings.
    /// </summary>
    public sealed class MenuSettingsPage
    {
        enum Category { Screen, Sound, Gameplay, Controls, Access }

        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        readonly DisplaySettings _display;
        readonly Action _backgroundChanged;
        readonly List<Vector2Int> _resolutions;
        readonly Dictionary<Category, (VisualElement item, MenuIcon icon, Label label)> _items = new Dictionary<Category, (VisualElement, MenuIcon, Label)>();
        readonly VisualElement _nav;
        readonly VisualElement _indicator;
        readonly VisualElement _content;
        readonly SettingsPreview _preview;
        readonly Label _previewNote;
        Category _category = Category.Screen;
        UnityEngine.UIElements.Button _reset;
        bool _resetArmed;

        public VisualElement Root { get; }

        public MenuSettingsPage(CityHighwaysApp app, GameSetup setup, DisplaySettings display, Action back, Action backgroundChanged)
        {
            _app = app;
            _setup = setup;
            _display = display;
            _backgroundChanged = backgroundChanged;
            _resolutions = DisplaySettings.AvailableResolutions();
            Root = Ui.Column();

            var header = Ui.Row();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 14f;
            var backButton = Ui.Button(LocalizationService.Get("menu.settings.back"), back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            header.Add(backButton);
            var title = Ui.Text(LocalizationService.Get("menu.settings.title"), 34f, Theme.Gold, true);
            title.style.flexGrow = 1f;
            header.Add(title);
            _reset = Ui.Button(LocalizationService.Get("menu.settings.reset"), ResetAll, ButtonKind.Ghost, 14f);
            header.Add(_reset);
            Root.Add(header);

            // Быстрые пресеты.
            var presets = Ui.Row();
            presets.style.marginBottom = 12f;
            foreach (DisplaySettings.Preset preset in Enum.GetValues(typeof(DisplaySettings.Preset)))
                presets.Add(PresetCard(preset));
            Root.Add(presets);

            var body = Ui.Row();
            body.style.alignItems = Align.FlexStart;
            Root.Add(body);

            _nav = Ui.Column();
            _nav.style.width = 210f;
            _nav.style.marginRight = 14f;
            _nav.style.backgroundColor = Theme.Panel;
            Ui.Radius(_nav, 12f);
            Ui.Padding(_nav, 8f, 8f);
            body.Add(_nav);
            AddCategory(Category.Screen, MenuIcon.Glyph.Monitor, LocalizationService.Get("menu.settings.screen"));
            AddCategory(Category.Sound, MenuIcon.Glyph.Speaker, LocalizationService.Get("menu.settings.sound"));
            AddCategory(Category.Gameplay, MenuIcon.Glyph.Gamepad, LocalizationService.Get("menu.settings.gameplay"));
            AddCategory(Category.Controls, MenuIcon.Glyph.Keyboard, LocalizationService.Get("menu.settings.controls"));
            AddCategory(Category.Access, MenuIcon.Glyph.Access, LocalizationService.Get("menu.settings.access"));
            _indicator = Ui.Box();
            _indicator.pickingMode = PickingMode.Ignore;
            _indicator.style.position = Position.Absolute;
            _indicator.style.left = 2f;
            _indicator.style.width = 4f;
            _indicator.style.backgroundColor = GameColors.Accent;
            Ui.Radius(_indicator, 2f);
            Ui.Transitions(_indicator, 0.25f, "top", "height");
            _nav.Add(_indicator);
            _nav.RegisterCallback<GeometryChangedEvent>(_ => MoveIndicator());

            _content = Ui.Column();
            _content.style.flexGrow = 1f;
            _content.style.flexBasis = 0f;
            _content.style.marginRight = 14f;
            _content.style.backgroundColor = Theme.Panel;
            _content.style.minHeight = 470f;
            Ui.Radius(_content, 12f);
            Ui.Padding(_content, 18f, 22f);
            body.Add(_content);

            var previewCard = Ui.Column();
            previewCard.style.width = 340f;
            previewCard.style.backgroundColor = Theme.Panel;
            Ui.Radius(previewCard, 12f);
            Ui.Padding(previewCard, 14f, 14f);
            previewCard.Add(Ui.SectionTitle(LocalizationService.Get("menu.settings.preview")));
            _preview = new SettingsPreview(312f, 250f);
            previewCard.Add(_preview);
            _previewNote = Ui.Text("", 12f, Theme.TextDim);
            _previewNote.style.whiteSpace = WhiteSpace.Normal;
            _previewNote.style.marginTop = 8f;
            previewCard.Add(_previewNote);
            body.Add(previewCard);

            Rebuild();
        }

        /// <summary>Перечитать настройки (их могли поменять в партии или верхними значками меню) и перерисовать.</summary>
        public void Rebuild()
        {
            _resetArmed = false;
            _reset.text = LocalizationService.Get("menu.settings.reset");
            foreach (var pair in _items)
            {
                bool active = pair.Key == _category;
                pair.Value.item.style.backgroundColor = active ? new Color(GameColors.Accent.r, GameColors.Accent.g, GameColors.Accent.b, 0.16f) : Color.clear;
                pair.Value.icon.SetColor(active ? GameColors.Accent : Theme.TextDim);
                pair.Value.label.style.color = active ? GameColors.Accent : Theme.Text;
            }
            _content.Clear();
            var section = Ui.Column();
            switch (_category)
            {
                case Category.Screen: ScreenSection(section); break;
                case Category.Sound: SoundSection(section); break;
                case Category.Gameplay: GameplaySection(section); break;
                case Category.Controls: ControlsSection(section); break;
                default: AccessSection(section); break;
            }
            _content.Add(section);
            Ui.Appear(section);
            UpdatePreview(LocalizationService.Get("menu.settings.detail.001"));
            _nav.schedule.Execute(MoveIndicator).ExecuteLater(20);
        }

        void AddCategory(Category category, MenuIcon.Glyph glyph, string caption)
        {
            var item = Ui.Row();
            item.pickingMode = PickingMode.Position;
            item.style.alignItems = Align.Center;
            item.style.height = 54f;
            item.style.marginBottom = 4f;
            Ui.Padding(item, 0f, 14f);
            Ui.Radius(item, 10f);
            Ui.Transitions(item, "background-color", "translate");
            var icon = new MenuIcon(glyph, 28f, Theme.TextDim);
            icon.style.marginRight = 12f;
            item.Add(icon);
            var label = Ui.Text(caption, 17f, Theme.Text, true);
            item.Add(label);
            item.RegisterCallback<PointerEnterEvent>(_ =>
            {
                item.style.translate = new Translate(new Length(3f), new Length(0f));
                if (_category != category) item.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
                Ui.Hovered?.Invoke();
            });
            item.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                item.style.translate = new Translate(new Length(0f), new Length(0f));
                if (_category != category) item.style.backgroundColor = Color.clear;
            });
            item.RegisterCallback<ClickEvent>(_ =>
            {
                Ui.ButtonClicked?.Invoke();
                _category = category;
                Rebuild();
            });
            _items[category] = (item, icon, label);
            _nav.Add(item);
        }

        void MoveIndicator()
        {
            var item = _items[_category].item;
            if (float.IsNaN(item.worldBound.y)) return;
            _indicator.style.top = item.worldBound.y - _nav.worldBound.y + 10f;
            _indicator.style.height = item.worldBound.height - 20f;
        }

        VisualElement PresetCard(DisplaySettings.Preset preset)
        {
            var card = Ui.Row();
            card.pickingMode = PickingMode.Position;
            card.style.flexGrow = 1f;
            card.style.flexBasis = 0f;
            card.style.alignItems = Align.Center;
            card.style.marginRight = 10f;
            card.style.backgroundColor = Theme.Panel;
            Ui.Radius(card, 12f);
            Ui.Border(card, 1f, Theme.Line);
            Ui.Padding(card, 10f, 14f);
            Ui.Transitions(card, "background-color", "translate");
            var glyph = preset == DisplaySettings.Preset.LowEnd ? MenuIcon.Glyph.Monitor
                : preset == DisplaySettings.Preset.Cinematic ? MenuIcon.Glyph.Skyline : MenuIcon.Glyph.Flame;
            var icon = new MenuIcon(glyph, 30f, GameColors.Accent);
            icon.style.marginRight = 12f;
            card.Add(icon);
            var texts = Ui.Column();
            texts.style.flexShrink = 1f;
            texts.Add(Ui.Text(DisplaySettings.PresetTitle(preset), 15f, Theme.Text, true));
            var hint = Ui.Text(DisplaySettings.PresetHint(preset), 11f, Theme.TextDim);
            hint.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(hint);
            card.Add(texts);
            card.RegisterCallback<PointerEnterEvent>(_ =>
            {
                card.style.backgroundColor = Theme.PanelHover;
                card.style.translate = new Translate(new Length(0f), new Length(-2f));
                Ui.Hovered?.Invoke();
            });
            card.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                card.style.backgroundColor = Theme.Panel;
                card.style.translate = new Translate(new Length(0f), new Length(0f));
            });
            card.RegisterCallback<ClickEvent>(_ =>
            {
                Ui.ButtonClicked?.Invoke();
                _display.ApplyPreset(preset);
                Save();
                _backgroundChanged?.Invoke();
                Rebuild();
                UpdatePreview(string.Format(LocalizationService.Get("menu.settings.dynamic.preset"), DisplaySettings.PresetTitle(preset)));
            });
            return card;
        }

        // ================================================================== Разделы

        void ScreenSection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Monitor, LocalizationService.Get("menu.settings.screen")));
            // First localization slice: language choice is available from Settings too.
            // Rebuild the menu to refresh labels created programmatically by UI Toolkit.
            s.Add(Row(LocalizationService.Get("menu.settings.detail.002"), LocalizationService.Get("menu.settings.detail.003"),
                new Segmented(new[] { "Русский", "English" }, LocalizationService.Language == "en" ? 1 : 0, i =>
                {
                    LocalizationService.SetLanguage(i == 1 ? "en" : "ru");
                    _app.ShowMenu();
                })));

            s.Add(Row(LocalizationService.Get("menu.settings.detail.004"), LocalizationService.Get("menu.settings.detail.005"),
                new Segmented(new[] { LocalizationService.Get("menu.settings.detail.006"), LocalizationService.Get("menu.settings.detail.007") }, _display.fullscreen ? 0 : 1, i =>
                {
                    _display.fullscreen = i == 0;
                    Save();
                })));

            var names = _resolutions.Select(r => $"{r.x} × {r.y}").ToList();
            int current = Mathf.Max(0, _resolutions.FindIndex(r => r.x == _display.width && r.y == _display.height));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.008"), null, Stepper(names, current, i =>
            {
                _display.width = _resolutions[i].x;
                _display.height = _resolutions[i].y;
                Save();
            })));

            var levels = QualitySettings.names;
            if (levels != null && levels.Length > 1)
            {
                int quality = _display.quality >= 0 ? _display.quality : QualitySettings.GetQualityLevel();
                s.Add(Row(LocalizationService.Get("menu.settings.detail.009"), LocalizationService.Get("menu.settings.detail.010"), Stepper(levels.ToList(), quality, i =>
                {
                    _display.quality = i;
                    Save();
                    UpdatePreview(string.Format(LocalizationService.Get("menu.settings.dynamic.quality"), levels[i]));
                })));
            }

            s.Add(SubHead(LocalizationService.Get("menu.settings.detail.011")));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.012"), LocalizationService.Get("menu.settings.detail.013"),
                new Segmented(new[] { LocalizationService.Get("menu.settings.detail.014"), LocalizationService.Get("menu.settings.detail.015"), LocalizationService.Get("menu.settings.detail.016") }, _display.menuLight, i =>
                {
                    _display.menuLight = i;
                    Save();
                    _backgroundChanged?.Invoke();
                    UpdatePreview(i == 0 ? LocalizationService.Get("menu.settings.detail.017") : i == 1 ? LocalizationService.Get("menu.settings.detail.018") : LocalizationService.Get("menu.settings.detail.019"));
                })));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.020"), LocalizationService.Get("menu.settings.detail.021"),
                new Segmented(new[] { LocalizationService.Get("menu.palette.0"), LocalizationService.Get("menu.palette.1"), LocalizationService.Get("menu.palette.2") }, _display.menuPalette, i =>
                {
                    _display.menuPalette = i;
                    Save();
                    _backgroundChanged?.Invoke();
                    UpdatePreview(string.Format(LocalizationService.Get("menu.settings.dynamic.palette"), LocalizationService.Get("menu.palette." + i)));
                })));
            if (GeneratedArt.Load(GeneratedArt.KeyArt) != null)
                s.Add(Row(LocalizationService.Get("menu.settings.detail.022"), LocalizationService.Get("menu.settings.detail.023"),
                    new SwitchToggle(_display.menuKeyArt, on =>
                    {
                        _display.menuKeyArt = on;
                        Save();
                        _backgroundChanged?.Invoke();
                        UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.024") : LocalizationService.Get("menu.settings.detail.025"));
                    })));
            if (Application.isEditor)
                s.Add(Note(LocalizationService.Get("menu.settings.detail.026")));
        }

        void SoundSection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Speaker, LocalizationService.Get("menu.settings.sound")));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.027"), LocalizationService.Get("menu.settings.detail.028"), new SwitchToggle(_setup.sound, on =>
            {
                _setup.sound = on;
                _app.Sound.Enabled = on;
                _setup.Save();
                if (on) _app.Sound.Play(Sfx.Bell, 0.6f);
                UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.029") : LocalizationService.Get("menu.settings.detail.030"));
            })));

            // Громкость: слайдер, значок динамика меняется по уровню, число и тестовый звук.
            var volumeRow = Ui.Row();
            volumeRow.style.alignItems = Align.Center;
            var speaker = new MenuIcon(MenuIcon.Glyph.Speaker, 28f, GameColors.Accent);
            speaker.style.marginRight = 10f;
            volumeRow.Add(speaker);
            var value = Ui.Number("", 18f, Theme.Text, true);
            value.style.width = 56f;
            value.style.unityTextAlign = TextAnchor.MiddleRight;
            void Show(float v)
            {
                value.text = $"{Mathf.RoundToInt(v * 100f)}%";
                speaker.Level = v <= 0.001f ? 0 : v < 0.34f ? 1 : v < 0.67f ? 2 : 3;
            }
            var slider = new ValueSlider(220f, _display.volume, 0.05f, v =>
            {
                _display.volume = v;
                AudioListener.volume = v;
                Show(v);
            }, v =>
            {
                Save();
                _app.Sound.Play(Sfx.Bell, 0.6f);
                UpdatePreview(string.Format(LocalizationService.Get("menu.settings.dynamic.volume_test"), Mathf.RoundToInt(v * 100f)));
            });
            volumeRow.Add(slider);
            volumeRow.Add(value);
            Show(_display.volume);
            s.Add(Row(LocalizationService.Get("menu.settings.detail.031"), LocalizationService.Get("menu.settings.detail.032"), volumeRow));

            // Original loop is independent of SFX and opt-in for migrated saves.
            s.Add(Row(LocalizationService.Get("menu.settings.music"),
                LocalizationService.Get("menu.settings.music_hint"),
                new SwitchToggle(_display.musicEnabled, on =>
            {
                _display.musicEnabled = on;
                Save();
                _app.Music?.Sync(MusicSoundscape.ShouldPlay(on, _setup.sound,
                    _display.musicVolume), _display.musicVolume);
                UpdatePreview(LocalizationService.Get(on
                    ? "menu.settings.music_on" : "menu.settings.music_off"));
            })));

            var musicVolumeRow = Ui.Row();
            musicVolumeRow.style.alignItems = Align.Center;
            var musicValue = Ui.Number("", 16f, Theme.Text, true);
            musicValue.style.width = 54f;
            musicValue.style.unityTextAlign = TextAnchor.MiddleRight;
            void ShowMusicVolume(float valueNow) =>
                musicValue.text = Mathf.RoundToInt(valueNow * 100f) + "%";
            musicVolumeRow.Add(new ValueSlider(220f, _display.musicVolume, 0.05f, valueNow =>
            {
                _display.musicVolume = MusicSoundscape.SafeVolume(valueNow);
                ShowMusicVolume(_display.musicVolume);
                _app.Music?.Sync(MusicSoundscape.ShouldPlay(
                    _display.musicEnabled, _setup.sound, _display.musicVolume),
                    _display.musicVolume);
            }, _ =>
            {
                Save();
                UpdatePreview(LocalizationService.Get("menu.settings.music_volume_saved"));
            }));
            musicVolumeRow.Add(musicValue);
            ShowMusicVolume(_display.musicVolume);
            s.Add(Row(LocalizationService.Get("menu.settings.music_volume"),
                LocalizationService.Get("menu.settings.music_volume_hint"), musicVolumeRow));

            s.Add(Row(LocalizationService.Get("menu.settings.detail.033"), LocalizationService.Get("menu.settings.detail.034"), new SwitchToggle(_display.uiSounds, on =>
            {
                _display.uiSounds = on;
                Save();
            })));

            var test = Ui.Row();
            foreach (var (caption, sfx) in new[] { (LocalizationService.Get("menu.settings.detail.035"), Sfx.Card), (LocalizationService.Get("menu.settings.detail.036"), Sfx.Coin), (LocalizationService.Get("menu.settings.detail.037"), Sfx.Bell), (LocalizationService.Get("menu.settings.detail.038"), Sfx.Fanfare) })
            {
                var button = Ui.Button(caption, () => _app.Sound.Play(sfx), ButtonKind.Secondary, 13f);
                button.style.marginRight = 6f;
                test.Add(button);
            }
            s.Add(Row(LocalizationService.Get("menu.settings.detail.039"), null, test));
        }

        void GameplaySection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Gamepad, LocalizationService.Get("menu.settings.gameplay")));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.040"), LocalizationService.Get("menu.settings.detail.041"), new SwitchToggle(_display.tiltedBoard, on =>
            {
                _display.tiltedBoard = on;
                Save();
                UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.042") : LocalizationService.Get("menu.settings.detail.043"));
            })));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.044"), LocalizationService.Get("menu.settings.detail.045"), new SwitchToggle(_display.city3D, on =>
            {
                _display.city3D = on;
                Save();
                UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.046") : LocalizationService.Get("menu.settings.detail.047"));
            })));
            int angle = Mathf.Max(0, Array.IndexOf(DisplaySettings.TiltOptions, _display.tiltDegrees));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.048"), LocalizationService.Get("menu.settings.detail.049"),
                new Segmented(new[] { "30°", "38°", "45°" }, angle, i =>
                {
                    _display.tiltDegrees = DisplaySettings.TiltOptions[i];
                    Save();
                    UpdatePreview(string.Format(LocalizationService.Get("menu.settings.dynamic.camera_angle"), DisplaySettings.TiltOptions[i]));
                })));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.050"), LocalizationService.Get("menu.settings.detail.051"),
                new Segmented(DisplaySettings.City3DQualityNames, Mathf.Clamp(_display.city3DQuality, 0, 2), i =>
                {
                    _display.city3DQuality = i;
                    Save();
                    UpdatePreview(string.Format(LocalizationService.Get("menu.settings.dynamic.city_quality"), DisplaySettings.City3DQualityNames[i].ToLowerInvariant()));
                })));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.052"), LocalizationService.Get("menu.settings.detail.053"), new SwitchToggle(_display.showMinimap, on =>
            {
                _display.showMinimap = on;
                Save();
                UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.054") : LocalizationService.Get("menu.settings.detail.055"));
            })));
            var tutorial = Ui.Button(LocalizationService.Get("menu.settings.detail.056"), () => { }, ButtonKind.Secondary, 13f);
            tutorial.clicked += () =>
            {
                PlayerPrefs.DeleteKey(GameView.TutorialPrefsKey);
                PlayerPrefs.Save();
                tutorial.text = LocalizationService.Get("menu.settings.detail.057");
                Ui.SetEnabled(tutorial, false);
            };
            s.Add(Row(LocalizationService.Get("menu.settings.detail.058"), LocalizationService.Get("menu.settings.detail.059"), tutorial));
            s.Add(Note(LocalizationService.Get("menu.settings.detail.060")));
        }

        void ControlsSection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Keyboard, LocalizationService.Get("menu.settings.detail.061")));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.062"), LocalizationService.Get("menu.settings.detail.063"),
                new Segmented(DisplaySettings.SpeedNames, Mathf.Clamp(_display.cameraSpeed, 0, 2), i =>
                {
                    _display.cameraSpeed = i;
                    Save();
                    UpdatePreview(string.Format(LocalizationService.Get("menu.settings.dynamic.camera_speed"), DisplaySettings.SpeedNames[i].ToLowerInvariant()));
                })));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.064"), LocalizationService.Get("menu.settings.detail.065"), new SwitchToggle(_display.smoothCamera, on =>
            {
                _display.smoothCamera = on;
                Save();
                UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.066") : LocalizationService.Get("menu.settings.detail.067"));
            })));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.068"), LocalizationService.Get("menu.settings.detail.069") +
                      LocalizationService.Get("menu.settings.detail.070"), new SwitchToggle(_display.leftDragRotates, on =>
            {
                _display.leftDragRotates = on;
                Save();
                UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.071") : LocalizationService.Get("menu.settings.detail.072"));
                Rebuild();
            })));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.073"), LocalizationService.Get("menu.settings.detail.074"), new SwitchToggle(_display.invertTilt, on =>
            {
                _display.invertTilt = on;
                Save();
                UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.075") : LocalizationService.Get("menu.settings.detail.076"));
            })));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.077"), LocalizationService.Get("menu.settings.detail.078"),
                new Segmented(new[] { LocalizationService.Get("menu.settings.detail.079"), LocalizationService.Get("menu.settings.detail.080"), LocalizationService.Get("menu.settings.detail.081") }, Mathf.Clamp(_display.zoomSpeed, 0, 2), i =>
                {
                    _display.zoomSpeed = i;
                    Save();
                    UpdatePreview(LocalizationService.Get("menu.settings.detail.082"));
                })));

            s.Add(SubHead(LocalizationService.Get("menu.settings.detail.083")));
            foreach (var (keys, action) in new[]
            {
                (LocalizationService.Get("menu.settings.detail.084"), _display.leftDragRotates
                    ? LocalizationService.Get("menu.settings.detail.085")
                    : LocalizationService.Get("menu.settings.detail.086")),
                (LocalizationService.Get("menu.settings.detail.087"), _display.leftDragRotates
                    ? LocalizationService.Get("menu.settings.detail.088")
                    : LocalizationService.Get("menu.settings.detail.089")),
                (LocalizationService.Get("menu.settings.detail.090"), LocalizationService.Get("menu.settings.detail.091")),
                (LocalizationService.Get("menu.settings.detail.092"), LocalizationService.Get("menu.settings.detail.093")),
            }) s.Add(KeyRow(keys, action));
            s.Add(SubHead(LocalizationService.Get("menu.settings.detail.094")));
            foreach (var (keys, action) in new[]
            {
                ("Q / E", LocalizationService.Get("menu.settings.detail.095")),
                ("R / F", LocalizationService.Get("menu.settings.detail.096")),
                ("Home", LocalizationService.Get("menu.settings.detail.097")),
                ("Esc", LocalizationService.Get("menu.settings.detail.098")),
                ("← / →, Enter", LocalizationService.Get("menu.settings.detail.099")),
                ("F3", LocalizationService.Get("menu.settings.detail.100")),
            }) s.Add(KeyRow(keys, action));
        }

        /// <summary>Строка справки: клавиша в рамке и что она делает.</summary>
        static VisualElement KeyRow(string keys, string action)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 5f;
            row.style.paddingBottom = 5f;
            var key = Ui.Text(keys, 13f, Theme.Text, true);
            key.style.minWidth = 170f;
            key.style.marginRight = 12f;
            key.style.unityTextAlign = TextAnchor.MiddleCenter;
            key.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            Ui.Border(key, 1f, Theme.Line);
            Ui.Radius(key, 6f);
            Ui.Padding(key, 4f, 8f);
            row.Add(key);
            var text = Ui.Text(action, 13f, Theme.TextDim);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.flexShrink = 1f;
            row.Add(text);
            return row;
        }

        void AccessSection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Access, LocalizationService.Get("menu.settings.access")));
            s.Add(Row(LocalizationService.Get("menu.settings.detail.101"), LocalizationService.Get("menu.settings.detail.102"), new SwitchToggle(_display.reducedMotion, on =>
            {
                _display.reducedMotion = on;
                Save();
                UpdatePreview(on ? LocalizationService.Get("menu.settings.detail.103") : LocalizationService.Get("menu.settings.detail.104"));
            })));
            var scales = DisplaySettings.UiScales;
            int index = Array.FindIndex(scales, x => Mathf.Approximately(x, _display.uiScale));
            if (index < 0) index = 1;
            s.Add(Row(LocalizationService.Get("menu.settings.detail.105"), LocalizationService.Get("menu.settings.detail.106"),
                new Segmented(scales.Select(x => $"{Mathf.RoundToInt(x * 100f)}%").ToList(), index, i =>
                {
                    _display.uiScale = scales[i];
                    Save();
                    UpdatePreview(string.Format(LocalizationService.Get("menu.settings.dynamic.ui_scale"), Mathf.RoundToInt(scales[i] * 100f)));
                })));
            s.Add(Note(LocalizationService.Get("menu.settings.detail.107")));
        }

        // ================================================================== Строки и мелочи

        static VisualElement SectionHead(MenuIcon.Glyph glyph, string title)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 12f;
            var icon = new MenuIcon(glyph, 34f, GameColors.Accent);
            icon.style.marginRight = 12f;
            row.Add(icon);
            row.Add(Ui.Text(title, 24f, Theme.Text, true));
            return row;
        }

        static Label SubHead(string text)
        {
            var label = Ui.SectionTitle(text);
            label.style.marginTop = 14f;
            return label;
        }

        static Label Note(string text)
        {
            var label = Ui.Text(text, 12f, Theme.TextDim);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 12f;
            return label;
        }

        /// <summary>Строка настройки: подпись и пояснение слева, управление справа.</summary>
        static VisualElement Row(string caption, string hint, VisualElement control)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 10f;
            row.style.paddingBottom = 10f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;
            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            texts.style.flexShrink = 1f;
            texts.style.marginRight = 14f;
            texts.Add(Ui.Text(caption, 16f, Theme.Text, true));
            if (!string.IsNullOrEmpty(hint))
            {
                var label = Ui.Text(hint, 12f, Theme.TextDim);
                label.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(label);
            }
            row.Add(texts);
            control.style.flexShrink = 0f;
            row.Add(control);
            return row;
        }

        /// <summary>«‹ значение ›» — для длинных списков (разрешения, уровни качества).</summary>
        static VisualElement Stepper(List<string> values, int index, Action<int> changed)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            var label = Ui.Number("", 15f, Theme.Text, true);
            label.style.width = 150f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            int current = Mathf.Clamp(index, 0, values.Count - 1);
            void Show() => label.text = values.Count > 0 ? values[current] : "—";
            var left = ArrowButton(2, () => { current = (current - 1 + values.Count) % values.Count; Show(); Ui.Changed?.Invoke(); changed(current); });
            var right = ArrowButton(0, () => { current = (current + 1) % values.Count; Show(); Ui.Changed?.Invoke(); changed(current); });
            row.Add(left);
            row.Add(label);
            row.Add(right);
            Show();
            return row;
        }

        /// <summary>Кнопка со стрелкой-шевроном (level 2 — влево, 0 — вправо).</summary>
        static UnityEngine.UIElements.Button ArrowButton(int level, Action onClick)
        {
            var button = Ui.Button("", onClick, ButtonKind.Secondary, 14f);
            button.style.width = 40f;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            Ui.Padding(button, 0f, 0f);
            button.Add(new MenuIcon(MenuIcon.Glyph.Chevron, 18f, Theme.Text) { Level = level });
            return button;
        }

        void Save()
        {
            _display.Save();
            _display.Apply();
        }

        void UpdatePreview(string note)
        {
            _preview.Tilted = _display.tiltedBoard;
            _preview.Minimap = _display.showMinimap;
            _preview.ReducedMotion = _display.reducedMotion;
            _preview.Palette = _display.menuPalette;
            _preview.Light = _display.menuLight;
            _preview.UiScale = _display.uiScale;
            _preview.Quality = _display.quality;
            _preview.Refresh();
            if (note != null) _previewNote.text = note;
        }

        /// <summary>Сброс в два нажатия: всё, кроме прогресса и профилей, — к заводским значениям.</summary>
        void ResetAll()
        {
            if (!_resetArmed)
            {
                _resetArmed = true;
                _reset.text = LocalizationService.Get("menu.settings.reset_confirm");
                return;
            }
            _display.CopyFrom(DisplaySettings.Defaults());
            _setup.sound = true;
            _app.Sound.Enabled = true;
            _setup.Save();
            Save();
            _app.Music?.Sync(false, _display.musicVolume);
            _backgroundChanged?.Invoke();
            Rebuild();
            UpdatePreview(LocalizationService.Get("menu.settings.detail.108"));
        }
    }
}
