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
            var backButton = Ui.Button("← Назад", back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            header.Add(backButton);
            var title = Ui.Text("Настройки", 34f, Theme.Gold, true);
            title.style.flexGrow = 1f;
            header.Add(title);
            _reset = Ui.Button("Сбросить к заводским", ResetAll, ButtonKind.Ghost, 14f);
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
            AddCategory(Category.Screen, MenuIcon.Glyph.Monitor, "Экран");
            AddCategory(Category.Sound, MenuIcon.Glyph.Speaker, "Звук");
            AddCategory(Category.Gameplay, MenuIcon.Glyph.Gamepad, "Геймплей");
            AddCategory(Category.Controls, MenuIcon.Glyph.Keyboard, "Управление");
            AddCategory(Category.Access, MenuIcon.Glyph.Access, "Доступность");
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
            previewCard.Add(Ui.SectionTitle("Предпросмотр"));
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
            _reset.text = "Сбросить к заводским";
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
            UpdatePreview("Меняйте настройки — картинка справа и сама игра меняются сразу.");
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
                UpdatePreview($"Пресет «{DisplaySettings.PresetTitle(preset)}» применён. Громкость, режим экрана и разрешение не менялись.");
            });
            return card;
        }

        // ================================================================== Разделы

        void ScreenSection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Monitor, "Экран"));
            s.Add(Row("Режим", "Полный экран или окно.",
                new Segmented(new[] { "Полный экран", "В окне" }, _display.fullscreen ? 0 : 1, i =>
                {
                    _display.fullscreen = i == 0;
                    Save();
                })));

            var names = _resolutions.Select(r => $"{r.x} × {r.y}").ToList();
            int current = Mathf.Max(0, _resolutions.FindIndex(r => r.x == _display.width && r.y == _display.height));
            s.Add(Row("Разрешение", null, Stepper(names, current, i =>
            {
                _display.width = _resolutions[i].x;
                _display.height = _resolutions[i].y;
                Save();
            })));

            var levels = QualitySettings.names;
            if (levels != null && levels.Length > 1)
            {
                int quality = _display.quality >= 0 ? _display.quality : QualitySettings.GetQualityLevel();
                s.Add(Row("Качество графики", "Уровни из настроек проекта Unity: сглаживание, тени, детализация.", Stepper(levels.ToList(), quality, i =>
                {
                    _display.quality = i;
                    Save();
                    UpdatePreview($"Качество: {levels[i]}.");
                })));
            }

            s.Add(SubHead("Фон главного меню"));
            s.Add(Row("Свет", "По часам компьютера, всегда день или всегда ночь.",
                new Segmented(new[] { "По часам", "День", "Ночь" }, _display.menuLight, i =>
                {
                    _display.menuLight = i;
                    Save();
                    _backgroundChanged?.Invoke();
                    UpdatePreview(i == 0 ? "Свет по часам компьютера." : i == 1 ? "Всегда день." : "Всегда ночь: окна горят, в небе луна.");
                })));
            s.Add(Row("Палитра", "Цвета неба и окон — видно и на фоне меню за этой страницей.",
                new Segmented(MenuBackground.PaletteNames, _display.menuPalette, i =>
                {
                    _display.menuPalette = i;
                    Save();
                    _backgroundChanged?.Invoke();
                    UpdatePreview($"Палитра «{MenuBackground.PaletteNames[i]}».");
                })));
            if (GeneratedArt.Load(GeneratedArt.KeyArt) != null)
                s.Add(Row("Картина города", "Иллюстрация города вместо живого фона. Свет и палитра тогда не видны.",
                    new SwitchToggle(_display.menuKeyArt, on =>
                    {
                        _display.menuKeyArt = on;
                        Save();
                        _backgroundChanged?.Invoke();
                        UpdatePreview(on ? "На фоне меню — картина города." : "На фоне меню — живой город.");
                    })));
            if (Application.isEditor)
                s.Add(Note("В редакторе режим и разрешение задаёт окно Game — они применятся в собранной игре."));
        }

        void SoundSection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Speaker, "Звук"));
            s.Add(Row("Звук в игре", "Все звуки: карты, стройка, фанфары.", new SwitchToggle(_setup.sound, on =>
            {
                _setup.sound = on;
                _app.Sound.Enabled = on;
                _setup.Save();
                if (on) _app.Sound.Play(Sfx.Bell, 0.6f);
                UpdatePreview(on ? "Звук включён." : "Звук выключен.");
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
                UpdatePreview($"Громкость {Mathf.RoundToInt(v * 100f)}% — прозвучал тестовый звонок.");
            });
            volumeRow.Add(slider);
            volumeRow.Add(value);
            Show(_display.volume);
            s.Add(Row("Громкость", "Отпустите бегунок — прозвучит тестовый звук.", volumeRow));

            s.Add(Row("Звуки интерфейса", "Щелчки кнопок и тихие звуки при наведении на пункты меню.", new SwitchToggle(_display.uiSounds, on =>
            {
                _display.uiSounds = on;
                Save();
            })));

            var test = Ui.Row();
            foreach (var (caption, sfx) in new[] { ("Карта", Sfx.Card), ("Монета", Sfx.Coin), ("Звонок", Sfx.Bell), ("Фанфары", Sfx.Fanfare) })
            {
                var button = Ui.Button(caption, () => _app.Sound.Play(sfx), ButtonKind.Secondary, 13f);
                button.style.marginRight = 6f;
                test.Add(button);
            }
            s.Add(Row("Проверить звуки", null, test));
        }

        void GameplaySection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Gamepad, "Геймплей"));
            s.Add(Row("Наклон поля (2.5D)", "Поле с лёгким наклоном и глубиной; выключено — плоская карта сверху.", new SwitchToggle(_display.tiltedBoard, on =>
            {
                _display.tiltedBoard = on;
                Save();
                UpdatePreview(on ? "Поле наклонено: дальние дороги тоньше, ближние крупнее." : "Плоская карта сверху — всё одного масштаба.");
            })));
            s.Add(Row("3D-город", "Здания, деревья, река с набережными и мосты на опорах под картой; свет и тени по времени суток.", new SwitchToggle(_display.city3D, on =>
            {
                _display.city3D = on;
                Save();
                UpdatePreview(on ? "Под дорогами — объёмный город: тени, окна светятся ночью." : "Плоская нарисованная карта — легче для слабого ПК.");
            })));
            int angle = Mathf.Max(0, Array.IndexOf(DisplaySettings.TiltOptions, _display.tiltDegrees));
            s.Add(Row("Угол камеры", "Наклон камеры 3D-города: 30° — почти сверху, 45° — панорама.",
                new Segmented(new[] { "30°", "38°", "45°" }, angle, i =>
                {
                    _display.tiltDegrees = DisplaySettings.TiltOptions[i];
                    Save();
                    UpdatePreview($"Камера наклонена на {DisplaySettings.TiltOptions[i]:0}°.");
                })));
            s.Add(Row("Качество 3D", "Низкое — меньше зданий и без теней; высокое — мягкие тени, сглаживание и свечение.",
                new Segmented(DisplaySettings.City3DQualityNames, Mathf.Clamp(_display.city3DQuality, 0, 2), i =>
                {
                    _display.city3DQuality = i;
                    Save();
                    UpdatePreview($"Качество 3D-города: {DisplaySettings.City3DQualityNames[i].ToLowerInvariant()}.");
                })));
            s.Add(Row("Мини-карта", "Уменьшенная карта в углу поля: клик переносит обзор.", new SwitchToggle(_display.showMinimap, on =>
            {
                _display.showMinimap = on;
                Save();
                UpdatePreview(on ? "Мини-карта в правом нижнем углу." : "Мини-карта скрыта.");
            })));
            var tutorial = Ui.Button("Показать снова", () => { }, ButtonKind.Secondary, 13f);
            tutorial.clicked += () =>
            {
                PlayerPrefs.DeleteKey(GameView.TutorialPrefsKey);
                PlayerPrefs.Save();
                tutorial.text = "Покажем в новой партии";
                Ui.SetEnabled(tutorial, false);
            };
            s.Add(Row("Подсказки первого хода", "Наставник подскажет, что делать, в начале следующей партии.", tutorial));
            s.Add(Note("Камера, колесо мыши и горячие клавиши — в разделе «Управление»."));
        }

        void ControlsSection(VisualElement s)
        {
            s.Add(SectionHead(MenuIcon.Glyph.Keyboard, "Управление"));
            s.Add(Row("Скорость камеры", "Поворот и наклон 3D-поля мышью и клавишами Q/E, R/F.",
                new Segmented(DisplaySettings.SpeedNames, Mathf.Clamp(_display.cameraSpeed, 0, 2), i =>
                {
                    _display.cameraSpeed = i;
                    Save();
                    UpdatePreview($"Скорость камеры: {DisplaySettings.SpeedNames[i].ToLowerInvariant()}.");
                })));
            s.Add(Row("Плавное вращение", "Камера мягко догоняет мышь; выключено — поворачивается ровно за курсором.", new SwitchToggle(_display.smoothCamera, on =>
            {
                _display.smoothCamera = on;
                Save();
                UpdatePreview(on ? "Поворот поля плавный, без рывков." : "Поле поворачивается сразу за мышью.");
            })));
            s.Add(Row("Инвертировать наклон", "Мышь вверх — камера опускается к горизонту, вниз — поднимается.", new SwitchToggle(_display.invertTilt, on =>
            {
                _display.invertTilt = on;
                Save();
                UpdatePreview(on ? "Наклон инвертирован." : "Наклон как обычно: мышь вверх — вид сверху.");
            })));
            s.Add(Row("Приближение колесом", "Насколько сильно приближает один щелчок колеса мыши.",
                new Segmented(new[] { "Плавно", "Обычно", "Быстро" }, Mathf.Clamp(_display.zoomSpeed, 0, 2), i =>
                {
                    _display.zoomSpeed = i;
                    Save();
                    UpdatePreview("Шаг приближения колесом изменён.");
                })));

            s.Add(SubHead("Мышь"));
            foreach (var (keys, action) in new[]
            {
                ("Левая кнопка", "выбрать дорогу, станцию, груз; перетаскивание — сдвинуть карту"),
                ("Правая кнопка + движение", "повернуть (влево-вправо) и наклонить (вверх-вниз) 3D-поле"),
                ("Колесо", "приблизить или отдалить под курсором"),
            }) s.Add(KeyRow(keys, action));
            s.Add(SubHead("Клавиатура"));
            foreach (var (keys, action) in new[]
            {
                ("Q / E", "повернуть поле влево / вправо"),
                ("R / F", "наклонить камеру к горизонту / к виду сверху"),
                ("Home", "север сверху, наклон из настроек"),
                ("Esc", "в меню — назад, в предыдущий раздел"),
                ("← / →, Enter", "в выборе персонажа — листать и выбрать"),
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
            s.Add(SectionHead(MenuIcon.Glyph.Access, "Доступность"));
            s.Add(Row("Меньше анимаций", "Переходы почти мгновенные, контент появляется без сдвига, машинки в превью стоят.", new SwitchToggle(_display.reducedMotion, on =>
            {
                _display.reducedMotion = on;
                Save();
                UpdatePreview(on ? "Анимации сведены к минимуму." : "Анимации включены.");
            })));
            var scales = DisplaySettings.UiScales;
            int index = Array.FindIndex(scales, x => Mathf.Approximately(x, _display.uiScale));
            if (index < 0) index = 1;
            s.Add(Row("Масштаб интерфейса", "Весь интерфейс крупнее или мельче — меняется сразу.",
                new Segmented(scales.Select(x => $"{Mathf.RoundToInt(x * 100f)}%").ToList(), index, i =>
                {
                    _display.uiScale = scales[i];
                    Save();
                    UpdatePreview($"Масштаб интерфейса {Mathf.RoundToInt(scales[i] * 100f)}%.");
                })));
            s.Add(Note("Крупный масштаб на небольших экранах может не уместить длинные страницы — тогда их можно прокрутить."));
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
                _reset.text = "Точно сбросить? Нажмите ещё раз";
                return;
            }
            _display.CopyFrom(DisplaySettings.Defaults());
            _setup.sound = true;
            _app.Sound.Enabled = true;
            _setup.Save();
            Save();
            _backgroundChanged?.Invoke();
            Rebuild();
            UpdatePreview("Все настройки сброшены к заводским. Профили, статистика и сохранённая партия не тронуты.");
        }
    }
}
