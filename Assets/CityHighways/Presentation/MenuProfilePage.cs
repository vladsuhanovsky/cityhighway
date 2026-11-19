using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Профиль» из вкладок: Персонаж (гардероб с поворотной площадкой, примеркой и перетаскиванием вещей),
    /// Достижения (прогресс, фильтры, сортировка, группы), Кошелёк (история, график по дням, источники, прогноз уровня),
    /// Стиль (эмблемы, титулы, рамки с живым превью) и Стата (итоги партий и управление профилями).
    /// Профиль привязан к имени игрока: игрок с таким именем в «Новой игре» получает его оформление и пополняет кошелёк.
    /// </summary>
    public sealed partial class MenuProfilePage
    {
        enum Tab { Character, Achievements, Wallet, Style, Containers, Mastery, Stats }
        enum AchievementFilter { All, Active, Done }
        enum StyleFilter { All, Owned, Affordable }

        readonly GameSetup _setup;
        readonly Action _back;
        readonly Action _setupChanged;
        readonly Label _title;
        readonly Dictionary<Tab, (VisualElement button, MenuIcon icon, Label label)> _tabButtons = new Dictionary<Tab, (VisualElement, MenuIcon, Label)>();
        readonly ScrollView _scroll;
        const float ContentHeight = 664f;
        readonly VisualElement _content;
        readonly Label _statusLabel;
        readonly UnityEngine.UIElements.Button _idChip;
        Tab _tab = Tab.Character;
        string _status = "";

        // Персонаж
        string _slot = "hat";
        Outfit _tryOn;
        TurntableCharacter _turntable;
        string _animateSlot;
        VisualElement _ghost;
        CharacterItem _dragItem;
        VisualElement _pressTile;
        Vector2 _pressPosition;
        int _pressPointer = -1;
        bool _dragging, _justDragged;

        // Достижения
        AchievementFilter _achievementFilter = AchievementFilter.All;
        bool _sortByReward;
        readonly HashSet<string> _celebrating = new HashSet<string>();

        // Кошелёк
        string _historyFilter = "all";

        // Стиль
        StyleFilter _styleFilter = StyleFilter.All;
        ShopItem _preview;

        // Анимации текущей вкладки
        readonly List<AnimatedFrame> _frames = new List<AnimatedFrame>();
        readonly List<VisualElement> _pulsing = new List<VisualElement>();
        float _time;

        public VisualElement Root { get; }

        public MenuProfilePage(GameSetup setup, Action back, Action setupChanged)
        {
            _setup = setup;
            _back = back;
            _setupChanged = setupChanged;
            // Своя подложка страницы в палитре профиля (GameColors) поверх приглушённого города.
            Root = Ui.Column();
            Root.style.backgroundColor = new Color(GameColors.Background.r, GameColors.Background.g, GameColors.Background.b, 0.94f);
            Ui.Radius(Root, 16f);
            Ui.Padding(Root, 16f, 18f);

            var header = Ui.Row();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 12f;
            var backButton = Btn("← Назад", () => _back(), ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            header.Add(backButton);
            _title = Ui.Text("Профиль", 32f, GameColors.Accent, true);
            header.Add(_title);
            // ID игрока — тот же, что у профиля в Steam (по нему профиль хранится на сервере).
            _idChip = Ui.Button("", () =>
            {
                var id = Profiles.Active.PlatformId;
                if (id.Length == 0) return;
                GUIUtility.systemCopyBuffer = Profiles.Active.steamId;
                _status = "ID скопирован.";
                Rebuild();
            }, GameColors.Button, GameColors.ButtonHover, Theme.TextDim, 12f);
            _idChip.style.marginLeft = 14f;
            _idChip.style.minHeight = 28f;
            Ui.Padding(_idChip, 3f, 10f);
            _idChip.tooltip = "ID профиля Steam — нажмите, чтобы скопировать";
            header.Add(_idChip);
            var grow = Ui.Box();
            grow.style.flexGrow = 1f;
            header.Add(grow);
            _statusLabel = Ui.Text("", 14f, GameColors.Accent);
            header.Add(_statusLabel);
            Root.Add(header);

            // Вкладки вместо длинной прокрутки.
            var tabs = Ui.Row();
            tabs.style.marginBottom = 12f;
            AddTab(tabs, Tab.Character, MenuIcon.Glyph.Person, "Персонаж");
            AddTab(tabs, Tab.Achievements, MenuIcon.Glyph.Trophy, "Достижения");
            AddTab(tabs, Tab.Wallet, MenuIcon.Glyph.Coins, "Кошелёк");
            AddTab(tabs, Tab.Style, MenuIcon.Glyph.Palette, "Стиль");
            AddTab(tabs, Tab.Containers, MenuIcon.Glyph.Box, "Контейнеры");
            AddTab(tabs, Tab.Mastery, MenuIcon.Glyph.Cap, "Мастерство");
            AddTab(tabs, Tab.Stats, MenuIcon.Glyph.Chart, "Стата");
            Root.Add(tabs);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            // Высота у всех вкладок одна (как у «Достижений» и «Кошелька»): страница не прыгает при переключении,
            // короткие вкладки просто оставляют место снизу, длинные прокручиваются.
            _scroll.style.height = ContentHeight;
            _scroll.style.maxHeight = ContentHeight;
            _scroll.style.flexShrink = 0f;
            Ui.StyleScroller(_scroll);
            Root.Add(_scroll);
            _content = Ui.Column();
            _scroll.Add(_content);

            _turntable = new TurntableCharacter(230f);
            Rebuild();
        }

        void AddTab(VisualElement row, Tab tab, MenuIcon.Glyph glyph, string caption)
        {
            var button = Ui.Row();
            button.pickingMode = PickingMode.Position;
            button.style.marginRight = 8f;
            button.style.height = 44f;
            Ui.Padding(button, 0f, 16f);
            Ui.Radius(button, 10f);
            Ui.Transitions(button, "background-color", "translate");
            var icon = new MenuIcon(glyph, 20f, Theme.Text);
            icon.style.marginRight = 8f;
            button.Add(icon);
            var label = Ui.Text(caption, 16f, Theme.Text, true);
            button.Add(label);
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (_tab != tab) button.style.backgroundColor = GameColors.ButtonHover;
                button.style.translate = new Translate(new Length(0f), new Length(-1.5f));
                MenuButton.Hovered?.Invoke();
            });
            button.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                StyleTabs();
                button.style.translate = new Translate(new Length(0f), new Length(0f));
            });
            button.RegisterCallback<ClickEvent>(_ =>
            {
                Ui.ButtonClicked?.Invoke();
                _tab = tab;
                _status = "";
                _scroll.scrollOffset = Vector2.zero;
                Rebuild();
            });
            _tabButtons[tab] = (button, icon, label);
            row.Add(button);
        }

        void StyleTabs()
        {
            foreach (var pair in _tabButtons)
            {
                bool active = pair.Key == _tab;
                var ink = active ? GameColors.AccentText : Theme.Text;
                pair.Value.button.style.backgroundColor = active ? GameColors.Accent : GameColors.Button;
                pair.Value.icon.SetColor(ink);
                pair.Value.label.style.color = ink;
            }
        }

        public void Rebuild()
        {
            // Ушли с вкладки посреди открытия — вещь уже выдана, рулетку просто досматривать не нужно.
            if (_tab != Tab.Containers) FinishSpin(false);
            var profile = Profiles.Active;
            _title.text = "Профиль: " + profile.name;
            _idChip.text = profile.IsSteam ? $"ID Steam: {profile.steamId}" : "";
            Ui.Show(_idChip, profile.IsSteam);
            _statusLabel.text = _status;
            StyleTabs();
            _content.Clear();
            _frames.Clear();
            _pulsing.Clear();
            switch (_tab)
            {
                case Tab.Character: CharacterTab(profile); break;
                case Tab.Achievements: AchievementsTab(profile); break;
                case Tab.Wallet: WalletTab(profile); break;
                case Tab.Style: StyleTab(profile); break;
                case Tab.Containers: ContainersTab(profile); break;
                case Tab.Mastery: MasteryTab(profile); break;
                default: StatsTab(profile); break;
            }
            // В прокрутке блоки не должны сжиматься: иначе заголовки наезжают на содержимое.
            NoShrink(_content);
        }

        /// <summary>Анимации вкладки: поворот персонажа, рамки, «пульс» новых достижений.</summary>
        public void Tick(float dt)
        {
            _time += dt;
            if (_tab == Tab.Character) _turntable.Tick(dt);
            if (_tab == Tab.Containers) TickContainers(dt);
            foreach (var frame in _frames) frame.Tick(dt);
            float pulse = 1f + 0.03f * Mathf.Sin(_time * 5f);
            foreach (var element in _pulsing) element.style.scale = new Scale(new Vector3(pulse, pulse, 1f));
        }

        // ================================================================== Персонаж

        void CharacterTab(PlayerProfile profile)
        {
            var row = Ui.Row();
            // Карточки персонажа и гардероба — на всю высоту вкладки (она у всех вкладок одна).
            row.style.alignItems = Align.Stretch;
            row.style.minHeight = ContentHeight - 4f;
            _content.Add(row);

            var worn = profile.Outfit;
            var shown = _tryOn ?? worn;

            var left = Card(14f);
            left.style.alignItems = Align.Center;
            left.style.marginRight = 14f;
            left.style.width = 280f;
            left.style.justifyContent = Justify.Center;
            left.Add(Ui.Text("Персонаж", 18f, Theme.Text, true));
            var hint = Ui.Text("Тяните мышью — повернуть. Перетащите вещь на персонажа — надеть или примерить.", 12f, Theme.TextDim);
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginBottom = 8f;
            left.Add(hint);
            _turntable.RemoveFromHierarchy();
            _turntable.Set(shown, _animateSlot);
            _animateSlot = null;
            left.Add(_turntable);

            if (_tryOn != null)
            {
                var note = Ui.Text("Примерка — ещё не надето", 13f, GameColors.Warning, true);
                note.style.marginTop = 8f;
                left.Add(note);
                // Что из примеренного ещё не куплено и сколько стоит.
                var toBuy = CharacterCatalog.Slots.Select(s => CharacterCatalog.Item(_tryOn.Get(s)))
                    .Where(i => i != null && !profile.HasClothing(i)).ToList();
                int total = toBuy.Sum(i => i.price);
                if (toBuy.Count > 0)
                {
                    var buyAll = Btn($"Купить примеренное · {total}", () =>
                    {
                        foreach (var item in toBuy)
                        {
                            var error = Profiles.BuyClothing(profile, item);
                            if (error != null)
                            {
                                _status = error;
                                Rebuild();
                                return;
                            }
                        }
                        foreach (var slot in CharacterCatalog.Slots) Profiles.Wear(profile, slot, _tryOn.Get(slot));
                        _status = "Куплено и надето.";
                        _tryOn = null;
                        Rebuild();
                    }, ButtonKind.Primary, 14f);
                    buyAll.style.marginTop = 6f;
                    Ui.SetEnabled(buyAll, profile.wallet >= total);
                    left.Add(buyAll);
                }
                else
                {
                    var wearAll = Btn("Надеть примеренное", () =>
                    {
                        foreach (var slot in CharacterCatalog.Slots) Profiles.Wear(profile, slot, _tryOn.Get(slot));
                        _tryOn = null;
                        _status = "Надето.";
                        Rebuild();
                    }, ButtonKind.Primary, 14f);
                    wearAll.style.marginTop = 6f;
                    left.Add(wearAll);
                }
                var cancel = Btn("Снять примерку", () =>
                {
                    _tryOn = null;
                    Rebuild();
                }, ButtonKind.Ghost, 13f);
                cancel.style.marginTop = 6f;
                left.Add(cancel);
            }
            var wallet = Ui.Text($"Кошелёк: {profile.wallet} влияния", 14f, GameColors.Accent, true);
            wallet.style.marginTop = 10f;
            left.Add(wallet);
            row.Add(left);

            var right = Card(14f);
            right.style.flexGrow = 1f;
            right.style.flexShrink = 1f;
            string gender = shown.Gender;
            right.Add(GenderSwitch(profile, gender));
            var slots = Ui.Row();
            slots.style.flexWrap = Wrap.Wrap;
            foreach (var slot in CharacterCatalog.Slots)
            {
                string s = slot;
                var tab = Btn(CharacterCatalog.SlotTitle(slot), () => { _slot = s; Rebuild(); },
                    slot == _slot ? ButtonKind.Primary : ButtonKind.Secondary, 13f);
                tab.style.marginRight = 6f;
                tab.style.marginBottom = 6f;
                slots.Add(tab);
            }
            right.Add(slots);

            var grid = Ui.Row();
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.alignItems = Align.FlexStart;
            grid.style.marginTop = 6f;
            if (!CharacterCatalog.IsRequired(_slot)) grid.Add(ClothingTile(profile, null, worn));
            foreach (var item in CharacterCatalog.ItemsIn(_slot, gender).OrderBy(i => i.price)) grid.Add(ClothingTile(profile, item, worn));
            right.Add(grid);
            var legend = Ui.Row();
            legend.style.marginTop = 8f;
            legend.style.flexWrap = Wrap.Wrap;
            foreach (Rarity.Level level in Enum.GetValues(typeof(Rarity.Level)))
            {
                var dot = Ui.Box();
                dot.style.width = 10f;
                dot.style.height = 10f;
                dot.style.marginRight = 5f;
                dot.style.backgroundColor = Rarity.ColorOf(level);
                Ui.Radius(dot, 5f);
                legend.Add(dot);
                var label = Ui.Text(Rarity.Title(level), 12f, Theme.TextDim);
                label.style.marginRight = 14f;
                legend.Add(label);
            }
            right.Add(legend);
            row.Add(right);
        }

        /// <summary>
        /// Переключатель «Мужчина / Женщина»: у каждого пола своя фигура и свой гардероб. Наряд другого пола
        /// запоминается и возвращается при обратном переключении; купленные вещи остаются за профилем.
        /// </summary>
        VisualElement GenderSwitch(PlayerProfile profile, string gender)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 10f;
            var caption = Ui.Text("Персонаж:", 14f, Theme.TextDim, true);
            caption.style.marginRight = 10f;
            row.Add(caption);
            foreach (var g in new[] { CharacterCatalog.Male, CharacterCatalog.Female })
            {
                string chosen = g;
                bool active = g == gender;
                var button = Btn((g == CharacterCatalog.Male ? "♂ " : "♀ ") + CharacterCatalog.GenderTitle(g), () =>
                {
                    if (active) return;
                    Profiles.SetGender(profile, chosen);
                    _tryOn = null;
                    _animateSlot = "body";
                    _status = chosen == CharacterCatalog.Female ? "Женский персонаж и женский гардероб." : "Мужской персонаж и мужской гардероб.";
                    Rebuild();
                }, active ? ButtonKind.Primary : ButtonKind.Secondary, 14f);
                button.style.marginRight = 6f;
                button.style.minWidth = 130f;
                row.Add(button);
            }
            return row;
        }

        /// <summary>
        /// Вещь в гардеробе: мини-персонаж в ней, название, редкость (цвет рамки и плашка), цена и кнопка.
        /// Клик — примерить, перетащить на персонажа — надеть (или примерить, если ещё не куплено).
        /// item == null — «ничего» для необязательного слота.
        /// </summary>
        VisualElement ClothingTile(PlayerProfile profile, CharacterItem item, Outfit worn)
        {
            string id = item?.id ?? "";
            string slot = _slot;
            bool wearing = worn.Get(slot) == id;
            bool trying = _tryOn != null && _tryOn.Get(slot) == id && !wearing;
            bool has = item == null || profile.HasClothing(item);
            var rarity = Rarity.ColorOf(Rarity.Of(item?.price ?? 0));

            var tile = Ui.Column();
            tile.pickingMode = PickingMode.Position;
            tile.style.width = 124f;
            tile.style.marginRight = 8f;
            tile.style.marginBottom = 8f;
            tile.style.alignItems = Align.Center;
            tile.style.backgroundColor = wearing ? GameColors.ButtonHover : GameColors.Button;
            Ui.Radius(tile, 10f);
            Ui.Padding(tile, 6f, 4f);
            Ui.Border(tile, wearing || trying ? 2f : 1f, wearing ? GameColors.Accent : trying ? GameColors.Warning : new Color(rarity.r, rarity.g, rarity.b, 0.7f));
            tile.style.borderTopWidth = 4f;
            tile.style.borderTopColor = rarity;
            Ui.Transitions(tile, "translate", "background-color");

            var mini = new CharacterView(60f, slot == "hat" || slot == "hair" || slot == "body");
            mini.Set(worn.With(slot, id));
            tile.Add(mini);
            var name = Ui.Text(item?.name ?? "Без " + (slot == "hat" ? "головного убора" : "аксессуара"), 12f, Theme.Text, true);
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.marginTop = 4f;
            tile.Add(name);
            if (item != null)
            {
                var tag = Rarity.Tag(item.price);
                tag.style.marginTop = 3f;
                tile.Add(tag);
            }

            UnityEngine.UIElements.Button button;
            if (wearing)
            {
                button = Btn("Надето", () => { }, ButtonKind.Ghost, 12f);
                Ui.SetEnabled(button, false);
            }
            else if (has)
                button = Btn("Надеть", () => Apply(profile, item, slot, true), ButtonKind.Secondary, 12f);
            else
            {
                button = Btn($"Купить · {item.price}", () =>
                {
                    var error = Profiles.BuyClothing(profile, item);
                    _status = error ?? $"Куплено и надето: {item.name}.";
                    if (error == null)
                    {
                        _tryOn = null;
                        _animateSlot = slot;
                    }
                    Rebuild();
                }, ButtonKind.Primary, 12f);
                Ui.SetEnabled(button, profile.wallet >= item.price);
            }
            button.style.marginTop = 4f;
            tile.Add(button);

            tile.RegisterCallback<PointerEnterEvent>(_ => tile.style.translate = new Translate(new Length(0f), new Length(-3f)));
            tile.RegisterCallback<PointerLeaveEvent>(_ => tile.style.translate = new Translate(new Length(0f), new Length(0f)));
            // Клик — примерка; перетаскивание — на персонажа.
            tile.RegisterCallback<ClickEvent>(e =>
            {
                if (_justDragged)
                {
                    _justDragged = false;
                    return;
                }
                if (e.target is UnityEngine.UIElements.Button) return; // кнопка сама всё сделала
                Apply(profile, item, slot, false);
            });
            tile.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || e.target is UnityEngine.UIElements.Button) return;
                _pressTile = tile;
                _pressPosition = e.position;
                _pressPointer = e.pointerId;
                _dragItem = item;
            });
            tile.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_pressTile != tile || e.pointerId != _pressPointer) return;
                if (!_dragging && ((Vector2)e.position - _pressPosition).magnitude > 8f) StartDrag(tile, item, e.pointerId);
                if (_dragging) MoveGhost(e.position);
            });
            tile.RegisterCallback<PointerUpEvent>(e =>
            {
                if (_pressTile != tile || e.pointerId != _pressPointer) return;
                _pressTile = null;
                if (!_dragging) return;
                _dragging = false;
                _justDragged = true;
                Root.schedule.Execute(() => _justDragged = false).StartingIn(120);
                if (tile.HasPointerCapture(e.pointerId)) tile.ReleasePointer(e.pointerId);
                _ghost?.RemoveFromHierarchy();
                _ghost = null;
                Ui.Border(_turntable, 0f, Color.clear);
                if (_turntable.worldBound.Contains(e.position)) Apply(profile, _dragItem, slot, true);
            });
            return tile;
        }

        void StartDrag(VisualElement tile, CharacterItem item, int pointer)
        {
            _dragging = true;
            tile.CapturePointer(pointer);
            _ghost = Ui.Box();
            _ghost.style.position = Position.Absolute;
            _ghost.pickingMode = PickingMode.Ignore;
            _ghost.style.width = 80f;
            _ghost.style.height = 120f;
            _ghost.style.opacity = 0.85f;
            _ghost.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            Ui.Radius(_ghost, 10f);
            var texture = CharacterCatalog.Texture(item, (_tryOn ?? Profiles.Active.Outfit).Tone);
            if (texture != null)
            {
                _ghost.style.backgroundImage = new StyleBackground(texture);
                _ghost.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            }
            Root.Add(_ghost);
            // Подсказка, куда бросать.
            _turntable.style.borderTopWidth = 0f;
            Ui.Border(_turntable, 2f, new Color(GameColors.Accent.r, GameColors.Accent.g, GameColors.Accent.b, 0.8f));
            Ui.Radius(_turntable, 12f);
        }

        void MoveGhost(Vector2 position)
        {
            if (_ghost == null) return;
            var local = Root.WorldToLocal(position);
            _ghost.style.left = local.x - 40f;
            _ghost.style.top = local.y - 60f;
        }

        /// <summary>Надеть (если вещь есть) или примерить (если ещё не куплена); wear=false — всегда примерка.</summary>
        void Apply(PlayerProfile profile, CharacterItem item, string slot, bool wear)
        {
            string id = item?.id ?? "";
            bool has = item == null || profile.HasClothing(item);
            var worn = profile.Outfit;
            if (wear && has)
            {
                Profiles.Wear(profile, slot, id);
                if (_tryOn != null) _tryOn = _tryOn.With(slot, id);
                if (_tryOn != null && _tryOn.Serialize() == profile.Outfit.Serialize()) _tryOn = null;
                _status = item == null ? "Снято." : $"Надето: {item.name}.";
            }
            else
            {
                if (worn.Get(slot) == id && _tryOn == null) return;
                _tryOn = (_tryOn ?? worn).With(slot, id);
                if (_tryOn.Serialize() == worn.Serialize()) _tryOn = null;
                _status = item == null ? "" : has ? $"Примерка: {item.name}" : $"Примерка: {item.name} — купить за {item.price}";
            }
            _animateSlot = slot;
            Rebuild();
        }

        // ================================================================== Достижения

        void AchievementsTab(PlayerProfile profile)
        {
            int done = Achievements.All.Count(a => Achievements.Unlocked(profile, a));
            int rewardTaken = Achievements.All.Where(a => Achievements.Unlocked(profile, a)).Sum(a => a.Reward);

            var top = Ui.Row();
            top.style.alignItems = Align.Stretch;
            top.style.marginBottom = 12f;
            var summary = Card(14f);
            summary.style.width = 300f;
            summary.style.marginRight = 12f;
            summary.Add(Ui.Text($"Открыто {done} из {Achievements.All.Count}", 20f, Theme.Text, true));
            var bar = new ProgressBar(260f, 8f, GameColors.Accent);
            bar.Set(done / (float)Mathf.Max(1, Achievements.All.Count));
            bar.style.marginTop = 8f;
            summary.Add(bar);
            summary.Add(Caption($"Получено наград: {rewardTaken} влияния", 13f, Theme.TextDim, 0f, 6f));
            top.Add(summary);

            var task = Achievements.Today;
            bool todayDone = profile.dailyDone == Achievements.DateKey(DateTime.Now);
            var daily = Card(14f);
            daily.style.flexGrow = 1f;
            Ui.Border(daily, 2f, todayDone ? Theme.Line : GameColors.Accent);
            daily.Add(Ui.Text("Задание дня", 13f, GameColors.Accent, true));
            daily.Add(Ui.Text(task.Title, 17f, Theme.Text, true));
            daily.Add(Caption(todayDone ? "Выполнено сегодня" : $"Награда: +{Achievements.DailyReward} влияния · выполнено всего: {profile.dailyTotal}",
                13f, todayDone ? GameColors.Success : Theme.TextDim, 0f, 4f));
            top.Add(daily);
            _content.Add(top);

            // Фильтры и сортировка.
            var controls = Ui.Row();
            controls.style.marginBottom = 10f;
            controls.Add(Chip("Все", _achievementFilter == AchievementFilter.All, () => { _achievementFilter = AchievementFilter.All; Rebuild(); }));
            controls.Add(Chip("Активные", _achievementFilter == AchievementFilter.Active, () => { _achievementFilter = AchievementFilter.Active; Rebuild(); }));
            controls.Add(Chip("Завершённые", _achievementFilter == AchievementFilter.Done, () => { _achievementFilter = AchievementFilter.Done; Rebuild(); }));
            var space = Ui.Box();
            space.style.flexGrow = 1f;
            controls.Add(space);
            controls.Add(Caption("Сортировка:", 13f, Theme.TextDim, 8f));
            controls.Add(Chip("По прогрессу", !_sortByReward, () => { _sortByReward = false; Rebuild(); }));
            controls.Add(Chip("По награде", _sortByReward, () => { _sortByReward = true; Rebuild(); }));
            _content.Add(controls);

            bool anyNew = false;
            foreach (var category in Achievements.Categories)
            {
                var items = Achievements.All.Where(a => Achievements.CategoryOf(a) == category).Where(a =>
                {
                    bool unlocked = Achievements.Unlocked(profile, a);
                    return _achievementFilter == AchievementFilter.All || (_achievementFilter == AchievementFilter.Done) == unlocked;
                }).ToList();
                if (items.Count == 0) continue;
                items = _sortByReward
                    ? items.OrderByDescending(a => a.Reward).ToList()
                    : items.OrderByDescending(a => Fraction(profile, a)).ThenByDescending(a => a.Reward).ToList();
                int openedHere = items.Count(a => Achievements.Unlocked(profile, a));
                var title = Ui.SectionTitle($"{category} · {openedHere}/{items.Count}");
                title.style.marginTop = 4f;
                _content.Add(title);
                var grid = Ui.Row();
                grid.style.flexWrap = Wrap.Wrap;
                foreach (var achievement in items)
                {
                    grid.Add(AchievementCard(profile, achievement, out bool fresh));
                    anyNew |= fresh;
                }
                _content.Add(grid);
            }
            if (anyNew) Profiles.Save();
        }

        static float Fraction(PlayerProfile profile, Achievement achievement)
        {
            if (Achievements.Unlocked(profile, achievement)) return 1f;
            return achievement.Cumulative ? Achievements.ProgressOf(profile, achievement) / (float)Mathf.Max(1, achievement.Target) : 0f;
        }

        /// <summary>
        /// Карточка достижения: название, описание, полоска прогресса и награда. Только что открытое (ещё не видели)
        /// подсвечено бегущей золотой рамкой, «пульсирует» и помечено «НОВОЕ».
        /// </summary>
        VisualElement AchievementCard(PlayerProfile profile, Achievement achievement, out bool fresh)
        {
            bool unlocked = Achievements.Unlocked(profile, achievement);
            string seenMark = "seen:" + achievement.Id;
            fresh = unlocked && !profile.HasMark(seenMark);
            if (fresh)
            {
                profile.AddMark(seenMark);
                _celebrating.Add(achievement.Id);
            }
            bool celebrate = _celebrating.Contains(achievement.Id);

            var card = Ui.Column();
            card.style.width = 260f;
            card.style.marginRight = 10f;
            card.style.marginBottom = 10f;
            card.style.backgroundColor = unlocked ? GameColors.Button : GameColors.PanelBackground;
            Ui.Radius(card, 10f);
            Ui.Padding(card, 10f, 12f);
            Ui.Border(card, unlocked ? 2f : 1f, unlocked ? GameColors.Accent : Theme.Line);
            if (celebrate)
            {
                var frame = new AnimatedFrame(10f);
                frame.SetColor(GameColors.Accent);
                card.Add(frame);
                _frames.Add(frame);
                _pulsing.Add(card);
            }

            var head = Ui.Row();
            var name = Ui.Text(achievement.Title, 15f, unlocked ? GameColors.Accent : Theme.Text, true);
            name.style.flexGrow = 1f;
            name.style.flexShrink = 1f;
            head.Add(name);
            if (celebrate)
            {
                var tag = Ui.Text("НОВОЕ", 10f, GameColors.AccentText, true);
                tag.style.backgroundColor = GameColors.Warning;
                Ui.Radius(tag, 6f);
                Ui.Padding(tag, 2f, 6f);
                tag.style.marginRight = 6f;
                head.Add(tag);
            }
            head.Add(Ui.Number($"+{achievement.Reward}", 13f, unlocked ? Theme.TextDim : GameColors.Accent, true));
            card.Add(head);
            card.Add(Caption(achievement.Description, 12f, Theme.TextDim, 0f, 2f));

            int target = achievement.Cumulative ? achievement.Target : 1;
            int progress = unlocked ? target : achievement.Cumulative ? Achievements.ProgressOf(profile, achievement) : 0;
            var progressRow = Ui.Row();
            progressRow.style.marginTop = 8f;
            var bar = new ProgressBar(180f, 6f, unlocked ? GameColors.Success : GameColors.Accent);
            bar.Set(progress / (float)Mathf.Max(1, target));
            progressRow.Add(bar);
            var count = Ui.Number($"{progress}/{target}", 12f, unlocked ? GameColors.Success : Theme.TextDim, true);
            count.style.marginLeft = 8f;
            progressRow.Add(count);
            card.Add(progressRow);
            if (unlocked) card.Add(Caption("Получено", 11f, GameColors.Success, 0f, 4f));
            return card;
        }

        // ================================================================== Кошелёк

        static string SourceTitle(string source)
        {
            switch (source)
            {
                case "game": return "Партии";
                case "achievement": return "Достижения";
                case "daily": return "Задания дня";
                case "shop": return "Оформление";
                case "clothing": return "Одежда";
                case "container": return "Контейнеры";
                case "admin": return "От разработчика";
                case "mastery": return "Мастерство";
                default: return "Прочее";
            }
        }

        void WalletTab(PlayerProfile profile)
        {
            var history = profile.history ?? new List<WalletEntry>();
            int income = history.Where(h => h.amount > 0).Sum(h => h.amount);
            int spent = -history.Where(h => h.amount < 0).Sum(h => h.amount);

            var top = Ui.Row();
            top.style.alignItems = Align.Stretch;
            top.style.marginBottom = 12f;

            var balance = Card(16f);
            balance.style.flexGrow = 1f;
            balance.style.flexBasis = 0f;
            balance.style.marginRight = 12f;
            balance.Add(Ui.Text("В кошельке", 13f, Theme.TextDim, true));
            var amount = Ui.Number($"{profile.wallet}", 44f, GameColors.Accent, true);
            balance.Add(amount);
            balance.Add(Caption("влияния — тратится на одежду персонажа и оформление", 13f, Theme.TextDim, 0f, 0f));
            balance.Add(Caption($"Заработано всего: {profile.earnedTotal} · по истории: доход {income}, траты {spent}", 13f, Theme.Text, 0f, 8f));
            top.Add(balance);

            // Прогноз уровня: сколько опыта осталось и примерно сколько партий.
            int xp = ProfileLevel.Xp(profile);
            int level = ProfileLevel.Of(xp, out int into, out int need);
            var forecast = Card(16f);
            forecast.style.flexGrow = 1f;
            forecast.style.flexBasis = 0f;
            forecast.Add(Ui.Text("Прогноз уровня", 13f, Theme.TextDim, true));
            var levelRow = Ui.Row();
            levelRow.Add(Ui.Number($"Lvl {level}", 30f, ProfileLevel.TierColor(level), true));
            var rank = Ui.Text(ProfileLevel.Rank(level), 15f, Theme.Text, true);
            rank.style.marginLeft = 10f;
            levelRow.Add(rank);
            forecast.Add(levelRow);
            var levelBar = new ProgressBar(320f, 8f, GameColors.Success);
            levelBar.Set(into / (float)Mathf.Max(1, need));
            levelBar.style.marginTop = 6f;
            forecast.Add(levelBar);
            int left = need - into;
            float perGame = profile.games > 0 ? xp / (float)profile.games : ProfileLevel.XpPerGame;
            int games = Mathf.Max(1, Mathf.CeilToInt(left / Mathf.Max(1f, perGame)));
            forecast.Add(Caption($"До уровня {level + 1}: {left} опыта — примерно {games} {Plural(games, "партия", "партии", "партий")} (в среднем {perGame:0} опыта за партию).",
                13f, Theme.Text, 0f, 6f));
            forecast.Add(Caption($"Опыт: {ProfileLevel.XpPerGame} за партию, +{ProfileLevel.XpPerWin} за победу, +{ProfileLevel.XpPerAchievement} за достижение, +{ProfileLevel.XpPerDaily} за задание дня.",
                11f, Theme.TextDim, 0f, 4f));
            top.Add(forecast);
            _content.Add(top);

            // График по дням: последние 14 дней.
            var chartCard = Card(16f);
            chartCard.style.marginBottom = 12f;
            chartCard.Add(Ui.Text("Влияние по дням · 14 дней", 16f, Theme.Text, true));
            var chartLegend = Caption("зелёное — доход, красное — траты", 12f, Theme.TextDim, 0f, 2f);
            chartCard.Add(chartLegend);
            var chart = new EarningsChart(150f);
            chart.style.marginTop = 8f;
            var today = DateTime.Now.Date;
            var days = new List<(string, int, int)>();
            for (int i = 13; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                string key = day.ToString("yyyy-MM-dd");
                var entries = history.Where(h => h.Day == key).ToList();
                days.Add((day.ToString("dd.MM"), entries.Where(h => h.amount > 0).Sum(h => h.amount), -entries.Where(h => h.amount < 0).Sum(h => h.amount)));
            }
            chart.SetDays(days);
            chartCard.Add(chart);
            _content.Add(chartCard);

            var bottom = Ui.Row();
            bottom.style.alignItems = Align.FlexStart;

            // Источники: откуда пришло и куда ушло.
            var sources = Card(16f);
            sources.style.width = 340f;
            sources.style.marginRight = 12f;
            sources.Add(Ui.Text("Источники влияния", 16f, Theme.Text, true));
            int maxSource = 1;
            var groups = history.GroupBy(h => h.source).Select(g => (source: g.Key, sum: g.Sum(h => h.amount))).ToList();
            foreach (var g in groups) maxSource = Mathf.Max(maxSource, Mathf.Abs(g.sum));
            foreach (var source in new[] { "game", "achievement", "daily", "shop", "clothing", "container", "mastery" })
            {
                int sum = groups.Where(g => g.source == source).Sum(g => g.sum);
                var line = Ui.Row();
                line.style.marginTop = 8f;
                var caption = Ui.Text(SourceTitle(source), 13f, Theme.Text);
                caption.style.width = 110f;
                line.Add(caption);
                var sourceBar = new ProgressBar(140f, 8f, sum >= 0 ? GameColors.Success : GameColors.Danger);
                sourceBar.Set(Mathf.Abs(sum) / (float)maxSource);
                line.Add(sourceBar);
                var value = Ui.Number(sum > 0 ? $"+{sum}" : sum.ToString(), 13f, sum >= 0 ? GameColors.Success : GameColors.Danger, true);
                value.style.marginLeft = 8f;
                line.Add(value);
                sources.Add(line);
            }
            bottom.Add(sources);

            // История с фильтром.
            var list = Card(16f);
            list.style.flexGrow = 1f;
            list.style.flexShrink = 1f;
            var listHeader = Ui.Row();
            var listTitle = Ui.Text("История", 16f, Theme.Text, true);
            listTitle.style.flexGrow = 1f;
            listHeader.Add(listTitle);
            foreach (var (key, caption) in new[] { ("all", "Все"), ("income", "Доходы"), ("spent", "Траты"), ("game", "Партии"), ("achievement", "Достижения") })
            {
                string chosen = key;
                listHeader.Add(Chip(caption, _historyFilter == key, () => { _historyFilter = chosen; Rebuild(); }));
            }
            list.Add(listHeader);
            var filtered = history.Where(h =>
                _historyFilter == "all" ||
                (_historyFilter == "income" && h.amount > 0) ||
                (_historyFilter == "spent" && h.amount < 0) ||
                h.source == _historyFilter).Reverse().Take(60).ToList();
            if (filtered.Count == 0)
                list.Add(Caption(history.Count == 0
                    ? "История пуста. Она ведётся с этой версии игры: доходы с партий, награды и покупки появятся здесь."
                    : "Под этот фильтр записей нет.", 13f, Theme.TextDim, 0f, 8f));
            foreach (var entry in filtered)
            {
                var line = Ui.Row();
                line.style.marginTop = 6f;
                line.style.backgroundColor = GameColors.Background;
                Ui.Radius(line, 8f);
                Ui.Padding(line, 6f, 10f);
                var when = Ui.Number(entry.time ?? "", 12f, Theme.TextDim);
                when.style.width = 130f;
                line.Add(when);
                var kind = Ui.Text(SourceTitle(entry.source), 12f, Theme.TextDim, true);
                kind.style.width = 100f;
                line.Add(kind);
                var note = Ui.Text(entry.note ?? "", 13f, Theme.Text);
                note.style.flexGrow = 1f;
                note.style.flexShrink = 1f;
                line.Add(note);
                line.Add(Ui.Number(entry.amount > 0 ? $"+{entry.amount}" : entry.amount.ToString(), 14f, entry.amount > 0 ? GameColors.Success : GameColors.Danger, true));
                list.Add(line);
            }
            bottom.Add(list);
            _content.Add(bottom);
        }

        static string Plural(int n, string one, string few, string many)
        {
            int last = n % 10, lastTwo = n % 100;
            if (last == 1 && lastTwo != 11) return one;
            if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) return few;
            return many;
        }

        // ================================================================== Стиль: эмблемы, титулы, рамки

        void StyleTab(PlayerProfile profile)
        {
            // Живое превью: так вас видят за столом — с выбранным (или примеряемым) оформлением.
            string emblem = _preview != null && _preview.Kind == ShopKind.Emblem ? _preview.Id : profile.emblem;
            string title = _preview != null && _preview.Kind == ShopKind.Title ? _preview.Id : profile.title;
            string frameId = _preview != null && _preview.Kind == ShopKind.Frame ? _preview.Id : profile.frame;

            var previewCard = Card(16f);
            previewCard.style.marginBottom = 12f;
            previewCard.style.flexDirection = FlexDirection.Row;
            previewCard.style.alignItems = Align.Center;
            var side = Ui.Column();
            side.style.width = 260f;
            side.Add(Ui.Text("Живое превью", 16f, Theme.Text, true));
            side.Add(Caption(_preview == null
                ? "Так вас видят за столом. Нажмите на предмет — он появится здесь до покупки."
                : profile.Owns(_preview.Id)
                    ? $"Превью: «{_preview.Name}» — уже куплено, можно надеть."
                    : $"Превью: «{_preview.Name}» — ещё не куплено ({_preview.Price} влияния).", 12f, _preview == null ? Theme.TextDim : GameColors.Accent, 0f, 4f));
            if (_preview != null)
            {
                var reset = Btn("Показать надетое", () => { _preview = null; Rebuild(); }, ButtonKind.Ghost, 13f);
                reset.style.marginTop = 6f;
                reset.style.alignSelf = Align.FlexStart;
                side.Add(reset);
            }
            previewCard.Add(side);

            var stage = Ui.Column();
            stage.style.flexGrow = 1f;
            stage.style.alignItems = Align.Center;
            var row = Ui.Column();
            row.style.width = 380f;
            row.style.backgroundColor = GameColors.Background;
            Ui.Radius(row, 10f);
            Ui.Padding(row, 12f, 14f);
            var frame = new AnimatedFrame(10f);
            frame.SetColor(Profiles.FrameColor(frameId));
            _frames.Add(frame);
            row.Add(frame);
            var line = Ui.Row();
            var portrait = new CharacterView(44f, true);
            portrait.Set(profile.Outfit);
            portrait.style.marginRight = 10f;
            portrait.style.backgroundColor = GameColors.Background;
            Ui.Radius(portrait, 10f);
            line.Add(portrait);
            var badge = new ProfileBadge(22f);
            var frameColor = Profiles.FrameColor(frameId);
            badge.Set(emblem, frameColor.a > 0f ? frameColor : GameColors.Warning);
            badge.style.marginRight = 8f;
            line.Add(badge);
            var names = Ui.Column();
            names.Add(Ui.Text(profile.name, 20f, Theme.Text, true));
            var titleText = Profiles.TitleText(title);
            if (titleText.Length > 0) names.Add(Ui.Text(titleText, 13f, GameColors.Warning, true));
            line.Add(names);
            row.Add(line);
            stage.Add(row);
            previewCard.Add(stage);
            _content.Add(previewCard);

            var filters = Ui.Row();
            filters.style.marginBottom = 10f;
            filters.Add(Chip("Все", _styleFilter == StyleFilter.All, () => { _styleFilter = StyleFilter.All; Rebuild(); }));
            filters.Add(Chip("Купленные", _styleFilter == StyleFilter.Owned, () => { _styleFilter = StyleFilter.Owned; Rebuild(); }));
            filters.Add(Chip("Доступные для покупки", _styleFilter == StyleFilter.Affordable, () => { _styleFilter = StyleFilter.Affordable; Rebuild(); }));
            var space = Ui.Box();
            space.style.flexGrow = 1f;
            filters.Add(space);
            filters.Add(Ui.Text($"Кошелёк: {profile.wallet} влияния", 15f, GameColors.Accent, true));
            _content.Add(filters);

            StyleSection(profile, ShopKind.Emblem, "Эмблемы — у имени");
            StyleSection(profile, ShopKind.Title, "Титулы — под именем");
            StyleSection(profile, ShopKind.Frame, "Рамки — вокруг строки игрока");
        }

        void StyleSection(PlayerProfile profile, ShopKind kind, string caption)
        {
            var items = Profiles.Catalog.Where(i => i.Kind == kind).Where(i =>
                _styleFilter == StyleFilter.All ||
                (_styleFilter == StyleFilter.Owned && profile.Owns(i.Id)) ||
                (_styleFilter == StyleFilter.Affordable && !i.Exclusive && !profile.Owns(i.Id) && i.Price <= profile.wallet)).ToList();
            var title = Ui.SectionTitle($"{caption} · {items.Count}");
            _content.Add(title);
            if (items.Count == 0)
            {
                _content.Add(Caption(_styleFilter == StyleFilter.Owned ? "Пока ничего не куплено." : "Под этот фильтр ничего нет.", 13f, Theme.TextDim, 0f, 0f));
                return;
            }
            var grid = Ui.Row();
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginBottom = 8f;
            foreach (var item in items) grid.Add(StyleCell(profile, item));
            _content.Add(grid);
        }

        VisualElement StyleCell(PlayerProfile profile, ShopItem item)
        {
            bool owned = profile.Owns(item.Id);
            bool equipped = profile.Equipped(item.Kind) == item.Id;
            bool previewing = _preview == item;
            var rarity = Rarity.ColorOf(Rarity.Of(item.Price));

            var cell = Ui.Column();
            cell.pickingMode = PickingMode.Position;
            cell.style.width = 176f;
            cell.style.marginRight = 10f;
            cell.style.marginBottom = 10f;
            cell.style.alignItems = Align.Center;
            cell.style.backgroundColor = equipped ? GameColors.ButtonHover : GameColors.Button;
            Ui.Radius(cell, 10f);
            Ui.Padding(cell, 10f, 8f);
            Ui.Border(cell, previewing || equipped ? 2f : 1f, previewing ? GameColors.Warning : equipped ? GameColors.Accent : new Color(rarity.r, rarity.g, rarity.b, 0.7f));
            cell.style.borderTopWidth = 4f;
            cell.style.borderTopColor = rarity;
            Ui.Transitions(cell, "translate");

            if (item.Kind == ShopKind.Emblem)
            {
                var badge = new ProfileBadge(30f);
                badge.Set(item.Id, GameColors.Warning);
                cell.Add(badge);
            }
            else if (item.Kind == ShopKind.Frame)
            {
                // Рамка — сразу в движении, как за столом.
                var swatch = Ui.Box();
                swatch.style.width = 120f;
                swatch.style.height = 30f;
                swatch.style.backgroundColor = GameColors.Background;
                Ui.Radius(swatch, 8f);
                var animated = new AnimatedFrame(8f);
                animated.SetColor(Profiles.FrameColor(item.Id));
                swatch.Add(animated);
                _frames.Add(animated);
                cell.Add(swatch);
            }
            var name = Ui.Text(item.Name, 14f, item.Kind == ShopKind.Title ? GameColors.Warning : Theme.Text, true);
            name.style.marginTop = 6f;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            cell.Add(name);
            var tag = Rarity.Tag(item.Price);
            tag.style.marginTop = 4f;
            cell.Add(tag);

            UnityEngine.UIElements.Button button;
            if (equipped)
                button = Btn("Снять", () => { profile.Equip(item.Kind, ""); Profiles.Save(); _preview = null; Rebuild(); }, ButtonKind.Ghost, 13f);
            else if (owned)
                button = Btn("Надеть", () => { profile.Equip(item.Kind, item.Id); Profiles.Save(); _preview = null; _status = $"Надето: {item.Name}."; Rebuild(); }, ButtonKind.Secondary, 13f);
            else if (item.Mastery)
            {
                // Награда древа мастерства: ни купить, ни выиграть в контейнере.
                button = Btn("За мастерство", () => { _tab = Tab.Mastery; Rebuild(); }, ButtonKind.Secondary, 13f);
                button.tooltip = "Титул персонажа за ветку «Подмастерье» — вкладка «Мастерство».";
            }
            else if (item.Exclusive)
            {
                // В продаже не бывает: только из контейнеров.
                button = Btn("Из контейнера", () => { _tab = Tab.Containers; Rebuild(); }, ButtonKind.Secondary, 13f);
                button.tooltip = "Эта вещь выпадает только из контейнеров — вкладка «Контейнеры».";
            }
            else
            {
                button = Btn($"Купить · {item.Price}", () =>
                {
                    var error = Profiles.Buy(profile, item);
                    _status = error ?? $"Куплено: {item.Name}.";
                    if (error == null) _preview = null;
                    Rebuild();
                }, ButtonKind.Primary, 13f);
                Ui.SetEnabled(button, profile.wallet >= item.Price);
            }
            button.style.marginTop = 6f;
            cell.Add(button);

            cell.RegisterCallback<PointerEnterEvent>(_ => cell.style.translate = new Translate(new Length(0f), new Length(-3f)));
            cell.RegisterCallback<PointerLeaveEvent>(_ => cell.style.translate = new Translate(new Length(0f), new Length(0f)));
            // Клик по клетке — предпросмотр в живом превью (до покупки).
            cell.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is UnityEngine.UIElements.Button) return;
                _preview = previewing ? null : item;
                Rebuild();
            });
            return cell;
        }

        // ================================================================== Стата и профили

        void StatsTab(PlayerProfile profile)
        {
            int xp = ProfileLevel.Xp(profile);
            int level = ProfileLevel.Of(xp, out _, out _);
            var games = StatsLog.LoadAll()
                .Where(g => g.results != null)
                .Select(g => (game: g, me: g.results.FirstOrDefault(r => r.kind == "human" && string.Equals(r.name, profile.name, StringComparison.OrdinalIgnoreCase))))
                .Where(x => x.me != null).ToList();
            int best = games.Count > 0 ? games.Max(x => x.me.total) : 0;
            float average = games.Count > 0 ? (float)games.Average(x => x.me.total) : 0f;
            int contracts = games.Sum(x => x.me.completedContracts);
            int tolls = games.Sum(x => x.me.tollsEarned);
            int achievements = Achievements.All.Count(a => Achievements.Unlocked(profile, a));

            var tiles = Ui.Row();
            tiles.style.flexWrap = Wrap.Wrap;
            tiles.style.marginBottom = 8f;
            void Tile(string caption, string value, Color color, string note = null)
            {
                var tile = Card(14f);
                tile.style.width = 205f;
                tile.style.marginRight = 10f;
                tile.style.marginBottom = 10f;
                tile.Add(Ui.Text(caption, 12f, Theme.TextDim, true));
                tile.Add(Ui.Number(value, 28f, color, true));
                if (note != null) tile.Add(Caption(note, 11f, Theme.TextDim, 0f, 2f));
                tiles.Add(tile);
            }
            Tile("Уровень", $"{level}", ProfileLevel.TierColor(level), ProfileLevel.Rank(level));
            Tile("Партий", $"{profile.games}", Theme.Text);
            Tile("Побед", $"{profile.wins}", GameColors.Success, profile.games > 0 ? $"{100f * profile.wins / profile.games:0}% партий" : null);
            Tile("Лучший счёт", games.Count > 0 ? $"{best}" : "—", GameColors.Accent, games.Count > 0 ? $"в среднем {average:0}" : "по журналу партий");
            Tile("Контрактов выполнено", games.Count > 0 ? $"{contracts}" : "—", Theme.Text);
            Tile("Получено за проезд", games.Count > 0 ? $"{tolls}" : "—", Theme.Text);
            Tile("Достижений", $"{achievements}/{Achievements.All.Count}", GameColors.Accent);
            Tile("Заданий дня", $"{profile.dailyTotal}", Theme.Text);
            Tile("Влияния заработано", $"{profile.earnedTotal}", GameColors.Accent);
            _content.Add(tiles);

            if (games.Count > 0)
            {
                var recent = Card(14f);
                recent.style.marginBottom = 12f;
                recent.Add(Ui.Text("Последние партии", 16f, Theme.Text, true));
                foreach (var (game, me) in games.AsEnumerable().Reverse().Take(8))
                {
                    int place = game.results.Count(r => r.total > me.total) + 1;
                    var line = Ui.Row();
                    line.style.marginTop = 6f;
                    line.style.backgroundColor = GameColors.Background;
                    Ui.Radius(line, 8f);
                    Ui.Padding(line, 6f, 10f);
                    var date = Ui.Number(game.date ?? "", 12f, Theme.TextDim);
                    date.style.width = 150f;
                    line.Add(date);
                    var where = Ui.Text($"{place}-е место из {game.results.Length}" + (me.winner ? " · победа" : ""), 13f, me.winner ? GameColors.Success : Theme.Text, me.winner);
                    where.style.flexGrow = 1f;
                    line.Add(where);
                    line.Add(Ui.Number($"{me.total} очк", 14f, GameColors.Accent, true));
                    recent.Add(line);
                }
                _content.Add(recent);
            }

            ProfilesCard(profile);
        }

        /// <summary>Аватар, имя, список профилей этого компьютера, создание, удаление, «играть под этим именем».</summary>
        void ProfilesCard(PlayerProfile profile)
        {
            var card = Card(14f);
            card.style.marginBottom = 12f;
            card.Add(Ui.Text("Профиль и аватар", 16f, Theme.Text, true));

            var avatarRow = Ui.Row();
            avatarRow.style.alignItems = Align.Center;
            avatarRow.style.marginTop = 8f;
            var avatar = new AvatarView(84f);
            avatar.Set(profile);
            avatar.style.marginRight = 16f;
            avatarRow.Add(avatar);
            var avatarSide = Ui.Column();
            avatarSide.style.flexGrow = 1f;
            if (!profile.IsSteam)
            {
                var renameRow = Ui.Row();
                var renameField = Field(profile.name, 16);
                renameField.style.width = 280f;
                renameRow.Add(renameField);
                var rename = Btn("Сохранить имя", () =>
                {
                    string oldName = profile.name;
                    var error = Profiles.Rename(profile, renameField.value);
                    if (error == null)
                    {
                        // Если этим именем звали игрока в «Новой игре», переименуем и его.
                        foreach (var slot in _setup.players)
                            if (!slot.isBot && string.Equals(slot.name, oldName, StringComparison.OrdinalIgnoreCase)) slot.name = profile.name;
                        _setup.Save();
                        _setupChanged?.Invoke();
                    }
                    _status = error ?? $"Имя изменено: «{profile.name}».";
                    Rebuild();
                }, ButtonKind.Secondary, 14f);
                rename.style.marginLeft = 10f;
                renameRow.Add(rename);
                avatarSide.Add(renameRow);
            }
            else avatarSide.Add(Ui.Text(profile.name, 20f, Theme.Text, true));
            avatarSide.Add(Caption(profile.IsSteam
                ? "Профиль Steam: ник и аватар берутся из Steam. Можно поставить и свою картинку — она заменит аватар Steam."
                : "Загрузите картинку PNG или JPG — она обрежется до квадрата по центру.", 12f, Theme.TextDim, 0f, 4f));
            var avatarButtons = Ui.Row();
            avatarButtons.style.marginTop = 8f;
            if (FilePicker.Supported)
            {
                avatarButtons.Add(Btn("Загрузить картинку…", () =>
                {
                    string path = FilePicker.PickImage();
                    if (path == null) return;
                    _status = Profiles.ImportAvatar(profile, path) ?? "Аватар обновлён.";
                    Rebuild();
                }, ButtonKind.Primary, 14f));
            }
            else
            {
                var pathField = Field("", 260);
                pathField.style.width = 340f;
                avatarButtons.Add(pathField);
                var load = Btn("Загрузить по пути", () =>
                {
                    _status = Profiles.ImportAvatar(profile, pathField.value) ?? "Аватар обновлён.";
                    Rebuild();
                }, ButtonKind.Primary, 14f);
                load.style.marginLeft = 8f;
                avatarButtons.Add(load);
            }
            if (!string.IsNullOrEmpty(profile.avatar))
            {
                var removeAvatar = Btn(profile.IsSteam ? "Вернуть аватар Steam" : "Убрать картинку", () =>
                {
                    Profiles.RemoveAvatar(profile);
                    _status = "Картинка убрана.";
                    Rebuild();
                }, ButtonKind.Ghost, 14f);
                removeAvatar.style.marginLeft = 10f;
                avatarButtons.Add(removeAvatar);
            }
            avatarSide.Add(avatarButtons);
            avatarRow.Add(avatarSide);
            card.Add(avatarRow);
            // Аватар Steam мог ещё не догрузиться — заглянем ещё раз.
            if (profile.IsSteam && Profiles.AvatarOf(profile) == null)
                avatar.schedule.Execute(() => avatar.Set(profile)).Every(500).Until(() => Profiles.AvatarOf(profile) != null);

            var chips = Ui.Row();
            chips.style.flexWrap = Wrap.Wrap;
            chips.style.marginTop = 12f;
            chips.Add(Caption("Профили:", 14f, Theme.TextDim, 10f));
            foreach (var other in Profiles.Book.profiles)
            {
                var chosen = other;
                bool active = other == profile;
                var chip = Btn(other.name, () => { Profiles.SetActive(chosen); _status = ""; _tryOn = null; _preview = null; Rebuild(); },
                    active ? ButtonKind.Primary : ButtonKind.Secondary, 14f);
                chip.style.marginRight = 8f;
                chip.style.marginBottom = 6f;
                chips.Add(chip);
            }
            card.Add(chips);

            var createRow = Ui.Row();
            createRow.style.marginTop = 6f;
            var nameField = Field("", 16);
            nameField.style.width = 260f;
            nameField.textEdition.placeholder = "Имя нового профиля";
            createRow.Add(nameField);
            var create = Btn("Новый профиль", () =>
            {
                string name = (nameField.value ?? "").Trim();
                if (name.Length == 0) { _status = "Введите имя нового профиля."; Rebuild(); return; }
                if (Profiles.Find(name) != null) { _status = "Такой профиль уже есть."; Rebuild(); return; }
                Profiles.SetActive(Profiles.GetOrCreate(name));
                _status = $"Профиль «{name}» создан.";
                Rebuild();
            }, ButtonKind.Secondary, 14f);
            create.style.marginLeft = 10f;
            createRow.Add(create);
            var play = Btn("Играть под этим именем", () =>
            {
                var first = _setup.players.FirstOrDefault(p => !p.isBot);
                if (first != null) first.name = profile.name;
                _setup.Save();
                _setupChanged?.Invoke();
                _status = $"В «Новой игре» первый игрок теперь «{profile.name}».";
                Rebuild();
            }, ButtonKind.Secondary, 14f);
            play.style.marginLeft = 10f;
            createRow.Add(play);
            if (Profiles.Book.profiles.Count > 1)
            {
                var remove = Btn("Удалить профиль", () => { Profiles.Remove(profile); _status = $"Профиль «{profile.name}» удалён."; Rebuild(); },
                    ButtonKind.Ghost, 14f);
                remove.style.marginLeft = 10f;
                createRow.Add(remove);
            }
            card.Add(createRow);
            card.Add(Caption("Влияние, которое осталось у вас к концу партии, попадает в кошелёк. За одним компьютером профиль узнаётся по имени игрока, в сетевой игре — это активный профиль.",
                12f, Theme.TextDim, 0f, 8f));
            _content.Add(card);
        }

        // ================================================================== Мелочи

        /// <summary>Кнопка в палитре профиля: главная — оранжевая с тёмным текстом, обычная — сине-серая, «призрак» — прозрачная.</summary>
        static UnityEngine.UIElements.Button Btn(string text, Action onClick, ButtonKind kind = ButtonKind.Primary, float fontSize = 16f)
        {
            switch (kind)
            {
                case ButtonKind.Primary: return Ui.Button(text, onClick, GameColors.Accent, GameColors.AccentHover, GameColors.AccentText, fontSize);
                case ButtonKind.Secondary: return Ui.Button(text, onClick, GameColors.Button, GameColors.ButtonHover, Theme.Text, fontSize);
                default: return Ui.Button(text, onClick, kind, fontSize);
            }
        }

        /// <summary>Карточка в палитре профиля.</summary>
        static VisualElement Card(float padding = 14f)
        {
            var card = Ui.Card(padding);
            card.style.backgroundColor = GameColors.PanelBackground;
            return card;
        }

        /// <summary>Кнопка-фильтр: активная — оранжевая, остальные — сине-серые.</summary>
        static UnityEngine.UIElements.Button Chip(string text, bool active, Action onClick)
        {
            var chip = Btn(text, onClick, active ? ButtonKind.Primary : ButtonKind.Secondary, 13f);
            chip.style.minHeight = 32f;
            chip.style.marginRight = 6f;
            Ui.Padding(chip, 4f, 12f);
            return chip;
        }

        /// <summary>В колонках детям запрещаем сжиматься по высоте; в строках не трогаем (там текст должен переноситься).</summary>
        static void NoShrink(VisualElement element)
        {
            bool row = element.style.flexDirection.keyword == StyleKeyword.Undefined
                       && element.style.flexDirection.value == FlexDirection.Row;
            foreach (var child in element.Children())
            {
                if (!row) child.style.flexShrink = 0f;
                NoShrink(child);
            }
        }

        /// <summary>Поле ввода в стиле меню: заметный фон, рамка и светлый текст.</summary>
        static TextField Field(string value, int maxLength)
        {
            var field = new TextField { value = value, maxLength = maxLength };
            field.style.fontSize = 16f;
            field.style.color = Theme.Text;
            Ui.Margin(field, 0f);
            var input = field.Q(className: TextField.inputUssClassName) ?? field;
            input.style.backgroundColor = GameColors.Background;
            input.style.color = Theme.Text;
            input.style.minHeight = 36f;
            Ui.Radius(input, 8f);
            Ui.Border(input, 1f, Theme.TextDim);
            Ui.Padding(input, 6f, 10f);
            return field;
        }

        static Label Caption(string text, float size, Color color, float marginRight, float marginTop = 0f)
        {
            var label = Ui.Text(text, size, color);
            label.style.marginRight = marginRight;
            label.style.marginTop = marginTop;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
