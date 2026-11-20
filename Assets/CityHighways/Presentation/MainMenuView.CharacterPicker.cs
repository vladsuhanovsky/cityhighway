using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// «Новая игра» (соревнование и кооператив): окно выбора персонажа для игрока. Слева — крупный герой в фирменном
    /// цвете (картинка Avatars3D/<id>_portrait.png) и сложность игры им (5 делений), справа — имя, девиз, способность с подсказкой
    /// и статистика побед этим персонажем. Сверху — лента из шести героев, стрелки ‹ › и клавиши ←/→ листают.
    /// Внизу: «Выбрать этого персонажа», «Случайный выбор», а также «Выбор из двух в начале» и «Без персонажа».
    /// Esc или клик мимо окна — закрыть без изменений.
    /// </summary>
    public sealed partial class MainMenuView
    {
        VisualElement _pickerOverlay;
        int _pickerSlot = -1;
        int _pickerIndex;
        Dictionary<string, (int played, int won)> _pickerStats;

        void OpenCharacterPicker(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _setup.players.Count) return;
            _pickerSlot = slotIndex;
            int current = _setup.players[slotIndex].character;
            var all = Characters.All;
            _pickerIndex = current > 0 ? Mathf.Max(0, all.ToList().IndexOf((CharacterKind)current)) : 0;
            _pickerStats = LoadCharacterStats();
            BuildCharacterPicker();
        }

        void CloseCharacterPicker()
        {
            _pickerOverlay?.RemoveFromHierarchy();
            _pickerOverlay = null;
            _pickerSlot = -1;
        }

        void PickCharacter(int value)
        {
            if (_pickerSlot < 0 || _pickerSlot >= _setup.players.Count) return;
            _setup.players[_pickerSlot].character = value;
            _lastCharacterSlot = _pickerSlot;
            Ui.ButtonClicked?.Invoke();
            CloseCharacterPicker();
            Refresh();
        }

        void FlipCharacter(int step)
        {
            int count = Characters.All.Count;
            _pickerIndex = ((_pickerIndex + step) % count + count) % count;
            BuildCharacterPicker();
        }

        /// <summary>Сыграно и побед этим персонажем — по людям в локальной статистике (соревнование и кооператив).</summary>
        static Dictionary<string, (int, int)> LoadCharacterStats()
        {
            var result = new Dictionary<string, (int, int)>();
            try
            {
                foreach (var game in StatsLog.LoadAll())
                {
                    if (game?.results == null || game.mode == "magnate") continue;
                    foreach (var player in game.results)
                    {
                        if (player == null || player.kind != "human" || string.IsNullOrEmpty(player.character)) continue;
                        result.TryGetValue(player.character, out var stat);
                        result[player.character] = (stat.Item1 + 1, stat.Item2 + (player.winner ? 1 : 0));
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(LocalizationService.Get("menu.characters.001") + e.Message);
            }
            return result;
        }

        void BuildCharacterPicker()
        {
            var slot = _setup.players[_pickerSlot];
            var kind = Characters.All[_pickerIndex];
            var avatar = Avatar3DCatalog.For(kind);
            Color accent = avatar?.accent ?? Theme.Gold;
            var config = new GameConfig();

            // У каждого персонажа свой звук: монетка, молоток, портфель, касса, печать, стальная балка.
            _app.Sound.Play(SoundBank.ForCharacter(kind), 0.9f);

            _pickerOverlay?.RemoveFromHierarchy();
            var overlay = new VisualElement { focusable = true };
            Ui.Fill(overlay);
            overlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.72f);
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.target == overlay) CloseCharacterPicker();
            });
            overlay.RegisterCallback<KeyDownEvent>(e =>
            {
                // Esc закрывает окно через общий обработчик меню (кнопка «← Назад» окна), здесь — только L.
                if (e.keyCode == KeyCode.LeftArrow) FlipCharacter(-1);
                else if (e.keyCode == KeyCode.RightArrow) FlipCharacter(1);
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) PickCharacter((int)kind);
                else return;
                e.StopPropagation();
            });
            _pickerOverlay = overlay;

            var panel = Ui.Column();
            panel.style.width = 820f;
            panel.style.maxWidth = Length.Percent(96f);
            panel.style.backgroundColor = Theme.Panel;
            Ui.Radius(panel, 16f);
            Ui.Border(panel, 1f, new Color(accent.r, accent.g, accent.b, 0.55f));
            Ui.Padding(panel, 16f, 20f);
            overlay.Add(panel);

            // Шапка: назад и заголовок.
            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 12f;
            var back = Ui.Button(LocalizationService.Get("menu.characters.002"), CloseCharacterPicker, ButtonKind.Ghost, 14f);
            back.style.marginRight = 12f;
            head.Add(back);
            var title = Ui.Text(LocalizationService.Get("menu.characters.003"), 20f, Theme.Text, true);
            title.style.flexGrow = 1f;
            head.Add(title);
            var who = Ui.Text(string.IsNullOrWhiteSpace(slot.name) ? $"Игрок {_pickerSlot + 1}" : slot.name, 14f, Palette.PlayerColor(_pickerSlot), true);
            head.Add(who);
            panel.Add(head);

            // Лента героев.
            var strip = Ui.Row();
            strip.style.justifyContent = Justify.Center;
            strip.style.marginBottom = 14f;
            for (int i = 0; i < Characters.All.Count; i++)
            {
                int index = i;
                var k = Characters.All[i];
                bool selected = i == _pickerIndex;
                var cell = Ui.Column();
                cell.pickingMode = PickingMode.Position;
                cell.style.alignItems = Align.Center;
                cell.style.width = 96f;
                Ui.Padding(cell, 6f, 4f);
                Ui.Radius(cell, 10f);
                cell.style.backgroundColor = selected ? Theme.PanelHover : new Color(0f, 0f, 0f, 0f);
                var face = new CharacterIcon(selected ? 48f : 42f, k, selected ? accent : Theme.TextDim);
                cell.Add(face);
                var label = Ui.Text(Characters.Title(k), 11f, selected ? Theme.Text : Theme.TextDim, selected);
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.marginTop = 4f;
                label.pickingMode = PickingMode.Ignore;
                cell.Add(label);
                cell.RegisterCallback<ClickEvent>(_ =>
                {
                    if (index == _pickerIndex) return;
                    _pickerIndex = index;
                    BuildCharacterPicker();
                });
                strip.Add(cell);
            }
            panel.Add(strip);

            // Тело: стрелка, карточка героя, карточка способностей, стрелка.
            var body = Ui.Row();
            body.style.alignItems = Align.Stretch;
            body.Add(Arrow("‹", () => FlipCharacter(-1)));

            var left = Ui.Column();
            left.style.width = 260f;
            left.style.flexShrink = 0f;
            left.style.alignItems = Align.Center;
            left.style.marginRight = 14f;
            Ui.Padding(left, 12f, 12f);
            Ui.Radius(left, 14f);
            left.style.backgroundColor = new Color(accent.r * 0.45f, accent.g * 0.45f, accent.b * 0.45f, 0.9f);
            Ui.Border(left, 1f, new Color(accent.r, accent.g, accent.b, 0.7f));
            left.Add(HeroPicture(kind, accent));
            int level = Characters.Difficulty(kind);
            var squares = Ui.Row();
            squares.style.marginTop = 10f;
            for (int i = 1; i <= 5; i++)
            {
                var square = new VisualElement { pickingMode = PickingMode.Ignore };
                square.style.width = 22f;
                square.style.height = 22f;
                square.style.marginLeft = 3f;
                square.style.marginRight = 3f;
                Ui.Radius(square, 4f);
                square.style.backgroundColor = i <= level ? accent : new Color(1f, 1f, 1f, 0.12f);
                Ui.Border(square, 1f, new Color(1f, 1f, 1f, i <= level ? 0.5f : 0.15f));
                squares.Add(square);
            }
            left.Add(squares);
            var levelText = Ui.Text($"Сложность: {Characters.DifficultyTitle(level)}", 13f, Theme.Text);
            levelText.style.marginTop = 6f;
            left.Add(levelText);
            body.Add(left);

            var right = Ui.Column();
            right.style.flexGrow = 1f;
            right.style.flexShrink = 1f;
            Ui.Padding(right, 14f, 16f);
            Ui.Radius(right, 14f);
            right.style.backgroundColor = Theme.PanelLight;

            var nameRow = Ui.Row();
            nameRow.style.alignItems = Align.Center;
            var icon = new CharacterIcon(34f, kind, accent);
            icon.style.marginRight = 10f;
            nameRow.Add(icon);
            nameRow.Add(Ui.Text(Characters.Title(kind).ToUpperInvariant(), 24f, Theme.Text, true));
            right.Add(nameRow);
            var quote = Ui.Text($"«{Characters.Quote(kind)}»", 15f, Theme.TextDim);
            quote.style.unityFontStyleAndWeight = FontStyle.Italic;
            quote.style.whiteSpace = WhiteSpace.Normal;
            quote.style.marginTop = 6f;
            right.Add(quote);

            right.Add(Divider());
            right.Add(SectionTitle(LocalizationService.Get("menu.characters.004"), accent));
            var ability = Ui.Text(Characters.Ability(kind, config), 15f, Theme.Text);
            ability.style.whiteSpace = WhiteSpace.Normal;
            ability.style.marginTop = 4f;
            right.Add(ability);
            var hint = Ui.Text(Characters.Hint(kind), 13f, Theme.TextDim);
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.marginTop = 6f;
            right.Add(hint);

            right.Add(Divider());
            right.Add(SectionTitle(LocalizationService.Get("menu.characters.005"), accent));
            _pickerStats.TryGetValue(Characters.Title(kind), out var stat);
            string statText = stat.played == 0
                ? LocalizationService.Get("menu.characters.006")
                : $"Сыграно: {stat.played}  |  Побед: {stat.won} ({Mathf.RoundToInt(100f * stat.won / stat.played)}%)";
            var stats = Ui.Text(statText, 15f, Theme.Text);
            stats.style.marginTop = 4f;
            right.Add(stats);
            if (stat.played > 0)
            {
                var bar = new VisualElement();
                bar.style.height = 6f;
                bar.style.marginTop = 6f;
                bar.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                Ui.Radius(bar, 3f);
                var fill = new VisualElement();
                fill.style.height = Length.Percent(100f);
                fill.style.width = Length.Percent(100f * stat.won / stat.played);
                fill.style.backgroundColor = accent;
                Ui.Radius(fill, 3f);
                bar.Add(fill);
                right.Add(bar);
            }
            body.Add(right);
            // Текст карточки появляется вслед за аватаром, строка за строкой.
            long delay = 160;
            foreach (var child in right.Children())
            {
                Ui.Appear(child, delay);
                delay += 45;
            }
            body.Add(Arrow("›", () => FlipCharacter(1)));
            panel.Add(body);

            // Низ: выбрать, случайный, а также «выбор из двух» и «без персонажа».
            var foot = Ui.Row();
            foot.style.alignItems = Align.Center;
            foot.style.marginTop = 16f;
            bool chosen = slot.character == (int)kind;
            var choose = Ui.Button(chosen ? LocalizationService.Get("menu.characters.007") : LocalizationService.Get("menu.characters.008"), () => PickCharacter((int)kind), ButtonKind.Primary, 15f);
            choose.style.marginRight = 10f;
            foot.Add(choose);
            var random = Ui.Button(LocalizationService.Get("menu.characters.009"), () => PickCharacter(PlayerSlot.RandomCharacter),
                slot.character == PlayerSlot.RandomCharacter ? ButtonKind.Primary : ButtonKind.Secondary, 15f);
            random.style.marginRight = 10f;
            foot.Add(random);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            foot.Add(spacer);
            var draft = Ui.Button(LocalizationService.Get("menu.characters.010"), () => PickCharacter(PlayerSlot.DraftCharacter),
                slot.character == PlayerSlot.DraftCharacter ? ButtonKind.Secondary : ButtonKind.Ghost, 13f);
            draft.style.marginRight = 6f;
            foot.Add(draft);
            foot.Add(Ui.Button(LocalizationService.Get("menu.characters.011"), () => PickCharacter(0), slot.character == 0 ? ButtonKind.Secondary : ButtonKind.Ghost, 13f));
            panel.Add(foot);

            Root.Add(overlay);
            overlay.schedule.Execute(() => overlay.Focus());
        }

        /// <summary>Картинка героя для карточки (Avatars3D/&lt;id&gt;_portrait.png) — с лёгким появлением; нет картинки — значок.</summary>
        static VisualElement HeroPicture(CharacterKind kind, Color accent)
        {
            var portrait = Avatar3DCatalog.Portrait(kind);
            if (portrait == null)
            {
                var box = new VisualElement();
                box.style.width = 236f;
                box.style.height = 300f;
                box.style.alignItems = Align.Center;
                box.style.justifyContent = Justify.Center;
                box.Add(new CharacterIcon(160f, kind, accent));
                Ui.PopIn(box);
                return box;
            }
            var picture = new VisualElement { pickingMode = PickingMode.Ignore };
            picture.style.width = 236f;
            picture.style.height = 330f;
            picture.style.backgroundImage = new StyleBackground(portrait);
            picture.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            // Аватар «выпрыгивает»: 0 → 1,1 → 1.
            Ui.PopIn(picture);
            return picture;
        }

        static VisualElement Arrow(string glyph, System.Action action)
        {
            var arrow = Ui.Button(glyph, () => action(), ButtonKind.Ghost, 28f);
            arrow.style.width = 40f;
            arrow.style.alignSelf = Align.Center;
            arrow.style.marginLeft = 2f;
            arrow.style.marginRight = 2f;
            return arrow;
        }

        static VisualElement Divider()
        {
            var line = new VisualElement();
            line.style.height = 1f;
            line.style.marginTop = 12f;
            line.style.marginBottom = 10f;
            line.style.backgroundColor = Theme.Line;
            return line;
        }

        static VisualElement SectionTitle(string text, Color accent)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            var mark = new VisualElement();
            mark.style.width = 10f;
            mark.style.height = 10f;
            mark.style.marginRight = 8f;
            mark.style.backgroundColor = accent;
            Ui.Radius(mark, 2f);
            row.Add(mark);
            var label = Ui.Text(text, 13f, accent, true);
            label.style.letterSpacing = 1f;
            row.Add(label);
            return row;
        }
    }
}
