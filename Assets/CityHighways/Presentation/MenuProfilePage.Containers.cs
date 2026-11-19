using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Вкладка профиля «Контейнеры»: три вида контейнеров за влияние, у каждого — шансы редкостей. Вещь выдаётся
    /// сразу при открытии (Containers.Open), а лента-рулетка только показывает, что выпало: карточки едут влево
    /// и замедляются, выигрыш останавливается под золотой чертой. Ниже — итог с кнопкой «Надеть» и коллекция.
    /// </summary>
    public sealed partial class MenuProfilePage
    {
        const float SpinCard = 112f, SpinGap = 8f, SpinStep = SpinCard + SpinGap;
        const int SpinCount = 44, SpinWinner = 38;

        List<LootItem> _spinItems;
        ContainerResult _lastResult;
        bool _spinActive;
        float _spinTime, _spinDuration, _spinJitter;
        int _spinTicked = -1;
        VisualElement _spinWindow, _spinStrip;
        CrateIcon _spinCrate;

        void ContainersTab(PlayerProfile profile)
        {
            var intro = Card(16f);
            intro.style.flexDirection = FlexDirection.Row;
            intro.style.alignItems = Align.Center;
            intro.style.marginBottom = 12f;
            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            texts.style.flexShrink = 1f;
            texts.Add(Ui.Text("Контейнеры", 18f, Theme.Text, true));
            texts.Add(Caption("Открывайте за влияние: внутри одежда персонажа и оформление. Вещей с пометкой «только из контейнеров» " +
                              "нет в продаже. Выпадает то, чего у вас ещё нет; если вся редкость собрана — дубликат, за него " +
                              "возвращается половина ценности.", 12f, Theme.TextDim, 12f, 4f));
            intro.Add(texts);
            var numbers = Ui.Column();
            numbers.style.alignItems = Align.FlexEnd;
            numbers.Add(Ui.Text($"Кошелёк: {profile.wallet} влияния", 15f, GameColors.Accent, true));
            numbers.Add(Caption($"Открыто контейнеров: {profile.containersOpened}", 12f, Theme.TextDim, 0f, 4f));
            intro.Add(numbers);
            _content.Add(intro);

            var row = Ui.Row();
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 4f;
            _spinCrate = null;
            foreach (var kind in Containers.Kinds) row.Add(ContainerCard(profile, kind));
            _content.Add(row);

            if (_spinItems != null) _content.Add(SpinWindow(profile));
            if (!_spinActive && _lastResult != null) _content.Add(ResultCard(profile, _lastResult));
            _content.Add(CollectionCard(profile));
        }

        VisualElement ContainerCard(PlayerProfile profile, ContainerKind kind)
        {
            var card = Card(14f);
            card.style.width = 296f;
            card.style.marginRight = 10f;
            card.style.marginBottom = 10f;
            card.style.alignItems = Align.Center;
            card.style.borderTopWidth = 4f;
            card.style.borderTopColor = kind.Color;
            var crate = new CrateIcon(116f, kind.Color);
            if (_spinActive && _lastResult != null && _lastResult.Container == kind) _spinCrate = crate;
            card.Add(crate);
            var name = Ui.Text(kind.Name, 18f, Theme.Text, true);
            name.style.marginTop = 6f;
            card.Add(name);
            var description = Caption(kind.Description, 12f, Theme.TextDim, 0f, 2f);
            description.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(description);

            // Шансы — открыто, по редкостям.
            var odds = Ui.Column();
            odds.style.alignSelf = Align.Stretch;
            odds.style.marginTop = 8f;
            for (int i = 0; i < kind.Odds.Length; i++)
            {
                if (kind.Odds[i] <= 0) continue;
                var color = Rarity.ColorOf((Rarity.Level)i);
                var line = Ui.Row();
                line.style.alignItems = Align.Center;
                line.style.marginTop = 2f;
                var dot = Ui.Box();
                dot.style.width = 9f;
                dot.style.height = 9f;
                dot.style.backgroundColor = color;
                Ui.Radius(dot, 4.5f);
                dot.style.marginRight = 6f;
                line.Add(dot);
                var label = Ui.Text(Containers.RarityNames[i], 12f, Theme.Text);
                label.style.flexGrow = 1f;
                line.Add(label);
                line.Add(Ui.Text($"{kind.Odds[i]}%", 12f, color, true));
                odds.Add(line);
            }
            card.Add(odds);

            var open = Btn($"Открыть · {kind.Price}", () => OpenContainer(profile, kind), ButtonKind.Primary, 14f);
            open.style.marginTop = 10f;
            open.style.alignSelf = Align.Stretch;
            Ui.SetEnabled(open, !_spinActive && profile.wallet >= kind.Price);
            if (profile.wallet < kind.Price) open.tooltip = $"Не хватает {kind.Price - profile.wallet} влияния.";
            card.Add(open);
            return card;
        }

        void OpenContainer(PlayerProfile profile, ContainerKind kind)
        {
            if (_spinActive) return;
            var error = Containers.Open(profile, kind, out var result);
            if (error != null)
            {
                _status = error;
                Rebuild();
                return;
            }
            // Лента: случайные вещи по шансам этого контейнера, выигрыш — на своём месте ближе к концу.
            var random = new System.Random();
            var pool = Containers.Pool(profile);
            _spinItems = new List<LootItem>();
            for (int i = 0; i < SpinCount; i++) _spinItems.Add(i == SpinWinner ? result.Item : Containers.RollAny(pool, kind, random));
            _lastResult = result;
            _spinActive = true;
            _spinTime = 0f;
            _spinTicked = -1;
            _spinJitter = (float)(random.NextDouble() - 0.5) * SpinCard * 0.7f;
            bool calm = DisplaySettings.Active != null && DisplaySettings.Active.reducedMotion;
            _spinDuration = calm ? 0.8f : 5.2f;
            _status = $"Открываем «{kind.Name}»…";
            _setupChanged?.Invoke();
            Rebuild();
        }

        VisualElement SpinWindow(PlayerProfile profile)
        {
            var window = new VisualElement();
            window.style.height = SpinCard + 44f;
            window.style.overflow = Overflow.Hidden;
            window.style.backgroundColor = GameColors.Background;
            window.style.marginBottom = 12f;
            Ui.Radius(window, 12f);
            Ui.Border(window, 1f, Theme.Line);
            var strip = Ui.Row();
            strip.style.position = Position.Absolute;
            strip.style.left = 0f;
            strip.style.top = 12f;
            foreach (var item in _spinItems)
            {
                var card = LootCard(profile, item, SpinCard);
                card.style.marginRight = SpinGap;
                strip.Add(card);
            }
            window.Add(strip);
            // Черта по центру и затемнение краёв.
            foreach (bool left in new[] { true, false })
            {
                var fade = new VisualElement { pickingMode = PickingMode.Ignore };
                fade.style.position = Position.Absolute;
                fade.style.top = 0f;
                fade.style.bottom = 0f;
                fade.style.width = 90f;
                if (left) fade.style.left = 0f; else fade.style.right = 0f;
                fade.style.backgroundColor = new Color(GameColors.Background.r, GameColors.Background.g, GameColors.Background.b, 0.55f);
                window.Add(fade);
            }
            var marker = new VisualElement { pickingMode = PickingMode.Ignore };
            marker.style.position = Position.Absolute;
            marker.style.top = 4f;
            marker.style.bottom = 4f;
            marker.style.width = 4f;
            marker.style.left = Length.Percent(50f);
            marker.style.translate = new Translate(new Length(-2f), new Length(0f));
            marker.style.backgroundColor = GameColors.Accent;
            Ui.Radius(marker, 2f);
            window.Add(marker);
            _spinWindow = window;
            _spinStrip = strip;
            window.RegisterCallback<GeometryChangedEvent>(_ => ApplySpin());
            return window;
        }

        /// <summary>Сдвиг ленты для текущего времени; щелчок звука, когда под чертой новая карточка.</summary>
        void ApplySpin()
        {
            if (_spinWindow == null || _spinStrip == null) return;
            float width = _spinWindow.layout.width;
            if (float.IsNaN(width) || width < 50f) return;
            float t = _spinActive ? Mathf.Clamp01(_spinTime / Mathf.Max(0.01f, _spinDuration)) : 1f;
            float ease = 1f - Mathf.Pow(1f - t, 4f);
            float final = width / 2f - (SpinWinner * SpinStep + SpinCard / 2f + _spinJitter);
            float offset = final * ease;
            _spinStrip.style.translate = new Translate(new Length(offset), new Length(0f));
            int under = Mathf.FloorToInt((width / 2f - offset) / SpinStep);
            if (_spinActive && under != _spinTicked)
            {
                if (_spinTicked >= 0) MenuButton.Hovered?.Invoke();
                _spinTicked = under;
            }
        }

        void TickContainers(float dt)
        {
            if (!_spinActive) return;
            _spinTime += dt;
            ApplySpin();
            // Контейнер, который открываем, подрагивает, пока крутится лента.
            if (_spinCrate != null)
            {
                float shake = Mathf.Sin(_spinTime * 38f) * 4f * (1f - Mathf.Clamp01(_spinTime / _spinDuration));
                _spinCrate.style.rotate = new Rotate(Angle.Degrees(shake));
            }
            if (_spinTime >= _spinDuration) FinishSpin(true);
        }

        void FinishSpin(bool rebuild)
        {
            if (!_spinActive) return;
            _spinActive = false;
            if (_lastResult != null)
                _status = _lastResult.Duplicate
                    ? $"Дубликат «{_lastResult.Item.Name}» — вернули {_lastResult.Refund} влияния."
                    : $"Выпало: {_lastResult.Item.Name}!";
            Ui.ButtonClicked?.Invoke();
            if (rebuild) Rebuild();
        }

        VisualElement ResultCard(PlayerProfile profile, ContainerResult result)
        {
            var item = result.Item;
            var color = Rarity.ColorOf(item.Level);
            var card = Card(16f);
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.marginBottom = 12f;
            Ui.Border(card, 2f, color);
            var big = LootCard(profile, item, 150f);
            big.style.marginRight = 16f;
            _pulsing.Add(big);
            card.Add(big);

            var side = Ui.Column();
            side.style.flexGrow = 1f;
            side.style.flexShrink = 1f;
            side.Add(Ui.Text(result.Duplicate ? "Уже есть в коллекции" : "Выпало!", 14f, Theme.TextDim, true));
            side.Add(Ui.Text(item.Name, 24f, color, true));
            var tags = Ui.Row();
            tags.style.marginTop = 4f;
            tags.style.alignItems = Align.Center;
            tags.Add(Rarity.Tag(item.Level));
            var kindLabel = Ui.Text(item.KindTitle, 12f, Theme.TextDim);
            kindLabel.style.marginLeft = 8f;
            tags.Add(kindLabel);
            if (item.Exclusive)
            {
                var exclusive = Ui.Text("ТОЛЬКО ИЗ КОНТЕЙНЕРОВ", 10f, GameColors.Warning, true);
                exclusive.style.marginLeft = 8f;
                tags.Add(exclusive);
            }
            side.Add(tags);
            if (result.Duplicate)
                side.Add(Caption($"Вместо дубликата в кошелёк вернулось {result.Refund} влияния.", 13f, GameColors.Accent, 0f, 6f));

            var buttons = Ui.Row();
            buttons.style.marginTop = 10f;
            if (Containers.Owns(profile, item))
            {
                bool worn = Containers.IsEquipped(profile, item);
                var wear = Btn(worn ? "Надето" : "Надеть", () =>
                {
                    Containers.Equip(profile, item);
                    _status = $"Надето: {item.Name}.";
                    _setupChanged?.Invoke();
                    Rebuild();
                }, ButtonKind.Secondary, 14f);
                Ui.SetEnabled(wear, !worn);
                wear.style.marginRight = 8f;
                buttons.Add(wear);
            }
            var again = Btn($"Открыть ещё · {result.Container.Price}", () => OpenContainer(profile, result.Container), ButtonKind.Primary, 14f);
            Ui.SetEnabled(again, profile.wallet >= result.Container.Price);
            buttons.Add(again);
            side.Add(buttons);
            card.Add(side);
            return card;
        }

        /// <summary>Сколько собрано по редкостям и из эксклюзивов.</summary>
        VisualElement CollectionCard(PlayerProfile profile)
        {
            var pool = Containers.Pool(profile);
            var card = Card(16f);
            card.Add(Ui.Text("Коллекция", 16f, Theme.Text, true));
            card.Add(Caption("Что из содержимого контейнеров у вас уже есть (одежда — для вашего пола персонажа).", 12f, Theme.TextDim, 0f, 2f));
            var row = Ui.Row();
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 8f;
            void Counter(string caption, Color color, int have, int total)
            {
                var box = Ui.Column();
                box.style.width = 150f;
                box.style.marginRight = 10f;
                box.style.marginBottom = 6f;
                box.Add(Ui.Text(caption, 12f, color, true));
                box.Add(Ui.Text($"{have} из {total}", 16f, Theme.Text, true));
                var bar = Ui.Box();
                bar.style.height = 6f;
                bar.style.marginTop = 4f;
                bar.style.backgroundColor = GameColors.Background;
                Ui.Radius(bar, 3f);
                var fill = Ui.Box();
                fill.style.height = 6f;
                fill.style.width = Length.Percent(total > 0 ? 100f * have / total : 0f);
                fill.style.backgroundColor = color;
                Ui.Radius(fill, 3f);
                bar.Add(fill);
                box.Add(bar);
                row.Add(box);
            }
            for (int i = 0; i < 4; i++)
            {
                var items = pool.Where(x => (int)x.Level == i).ToList();
                if (items.Count == 0) continue;
                Counter(Containers.RarityNames[i], Rarity.ColorOf((Rarity.Level)i), items.Count(x => Containers.Owns(profile, x)), items.Count);
            }
            var exclusives = pool.Where(x => x.Exclusive).ToList();
            Counter("Только из контейнеров", GameColors.Warning, exclusives.Count(x => Containers.Owns(profile, x)), exclusives.Count);
            card.Add(row);
            return card;
        }

        /// <summary>Карточка вещи: полоса редкости сверху, картинка (персонаж в этой вещи, эмблема, титул или рамка), имя.</summary>
        VisualElement LootCard(PlayerProfile profile, LootItem item, float width)
        {
            var color = Rarity.ColorOf(item.Level);
            var card = Ui.Column();
            card.pickingMode = PickingMode.Ignore;
            card.style.width = width;
            card.style.height = width;
            card.style.flexShrink = 0f;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.backgroundColor = GameColors.Button;
            Ui.Radius(card, 10f);
            Ui.Padding(card, 6f, 4f);
            Ui.Border(card, 1f, new Color(color.r, color.g, color.b, 0.7f));
            card.style.borderTopWidth = 5f;
            card.style.borderTopColor = color;

            var art = Ui.Column();
            art.style.flexGrow = 1f;
            art.style.justifyContent = Justify.Center;
            art.style.alignItems = Align.Center;
            if (item.Clothing != null)
            {
                string slot = item.Clothing.slot;
                var view = new CharacterView(width * 0.5f, slot == "hat" || slot == "hair" || slot == "body");
                view.Set(profile.Outfit.With(slot, item.Id));
                art.Add(view);
            }
            else if (item.Style.Kind == ShopKind.Emblem)
            {
                var badge = new ProfileBadge(width * 0.36f);
                badge.Set(item.Id, GameColors.Warning);
                art.Add(badge);
            }
            else if (item.Style.Kind == ShopKind.Title)
            {
                var title = Ui.Text(item.Name, Mathf.Max(11f, width * 0.11f), GameColors.Warning, true);
                title.style.whiteSpace = WhiteSpace.Normal;
                title.style.unityTextAlign = TextAnchor.MiddleCenter;
                art.Add(title);
            }
            else
            {
                var swatch = Ui.Box();
                swatch.style.width = width * 0.78f;
                swatch.style.height = width * 0.24f;
                swatch.style.backgroundColor = GameColors.Background;
                Ui.Radius(swatch, 8f);
                Ui.Border(swatch, 3f, Profiles.FrameColor(item.Id));
                art.Add(swatch);
            }
            card.Add(art);
            var name = Ui.Text(item.Name, width >= 140f ? 13f : 11f, Theme.Text, true);
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            name.style.whiteSpace = WhiteSpace.Normal;
            card.Add(name);
            return card;
        }
    }
}
