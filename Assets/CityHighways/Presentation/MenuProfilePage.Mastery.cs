using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Вкладка профиля «Мастерство»: древо прокачки каждого персонажа. Сверху — шесть персонажей с числом
    /// открытых узлов, ниже — древо выбранного: пять карточек-узлов, соединённых линиями (знакомство → ученик →
    /// подмастерье и победитель → мастер). У карточки — цель, полоска прогресса и награда.
    /// </summary>
    public sealed partial class MenuProfilePage
    {
        CharacterKind _masteryKind = CharacterKind.None;

        const float NodeWidth = 214f, NodeHeight = 118f, ColumnStep = 238f, RowStep = 96f;

        void MasteryTab(PlayerProfile profile)
        {
            // Если условия выполнены, а узел ещё не открыт (например, профиль со старой версии) — открыть сейчас.
            var fresh = new List<string>();
            foreach (var kind in Characters.All)
                foreach (var node in CharacterMastery.Unlock(profile, kind))
                    fresh.Add($"{CharacterLocalization.Title(kind)} — {node.Title}");
            if (fresh.Count > 0)
            {
                Profiles.Save();
                _status = string.Format(LocalizationService.Get("mastery.unlocked"), string.Join(", ", fresh));
                _statusLabel.text = _status;
                _setupChanged?.Invoke();
            }
            if (_masteryKind == CharacterKind.None)
            {
                // По умолчанию — персонаж, которым играли больше всего.
                _masteryKind = Characters.All.OrderByDescending(k => CharacterMastery.Progress(profile, k).games).First();
            }

            var intro = Card(14f);
            intro.style.marginBottom = 12f;
            intro.Add(Ui.Text(LocalizationService.Get("mastery.header"), 18f, Theme.Text, true));
            intro.Add(Caption(LocalizationService.Get("mastery.description"),
                12f, Theme.TextDim, 0f, 4f));
            var chips = Ui.Row();
            chips.style.flexWrap = Wrap.Wrap;
            chips.style.marginTop = 10f;
            foreach (var kind in Characters.All) chips.Add(MasteryChip(profile, kind));
            intro.Add(chips);
            _content.Add(intro);

            _content.Add(MasteryTreeCard(profile, _masteryKind));
        }

        static Color AccentOf(CharacterKind kind) => Avatar3DCatalog.For(kind)?.accent ?? GameColors.Accent;

        VisualElement MasteryChip(PlayerProfile profile, CharacterKind kind)
        {
            var accent = AccentOf(kind);
            bool selected = kind == _masteryKind;
            int done = CharacterMastery.DoneCount(profile, kind);
            var chip = Ui.Row();
            chip.pickingMode = PickingMode.Position;
            chip.style.alignItems = Align.Center;
            chip.style.width = 168f;
            chip.style.marginRight = 8f;
            chip.style.marginBottom = 8f;
            chip.style.backgroundColor = selected ? new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.95f) : GameColors.Button;
            Ui.Radius(chip, 10f);
            Ui.Border(chip, selected ? 2f : 1f, selected ? accent : Theme.Line);
            Ui.Padding(chip, 6f, 8f);
            var icon = new CharacterIcon(32f, kind, selected ? accent : Theme.TextDim);
            icon.pickingMode = PickingMode.Ignore;
            icon.style.marginRight = 8f;
            chip.Add(icon);
            var texts = Ui.Column();
            texts.pickingMode = PickingMode.Ignore;
            texts.style.flexGrow = 1f;
            var name = Ui.Text(CharacterLocalization.Title(kind), 13f, selected ? Theme.Text : Theme.TextDim, true);
            name.pickingMode = PickingMode.Ignore;
            texts.Add(name);
            var bar = Ui.Box();
            bar.pickingMode = PickingMode.Ignore;
            bar.style.height = 5f;
            bar.style.marginTop = 4f;
            bar.style.backgroundColor = GameColors.Background;
            Ui.Radius(bar, 2.5f);
            var fill = Ui.Box();
            fill.pickingMode = PickingMode.Ignore;
            fill.style.height = 5f;
            fill.style.width = Length.Percent(100f * done / 5f);
            fill.style.backgroundColor = accent;
            Ui.Radius(fill, 2.5f);
            bar.Add(fill);
            texts.Add(bar);
            chip.Add(texts);
            var count = Ui.Text($"{done}/5", 12f, done == 5 ? GameColors.Warning : Theme.TextDim, true);
            count.pickingMode = PickingMode.Ignore;
            count.style.marginLeft = 6f;
            chip.Add(count);
            chip.RegisterCallback<ClickEvent>(_ =>
            {
                if (_masteryKind == kind) return;
                Ui.ButtonClicked?.Invoke();
                _masteryKind = kind;
                Rebuild();
            });
            return chip;
        }

        VisualElement MasteryTreeCard(PlayerProfile profile, CharacterKind kind)
        {
            var accent = AccentOf(kind);
            var progress = CharacterMastery.Progress(profile, kind);
            var metric = CharacterMastery.Metric(kind);
            var card = Card(16f);
            card.style.borderTopWidth = 4f;
            card.style.borderTopColor = accent;

            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            var icon = new CharacterIcon(52f, kind, accent);
            icon.style.marginRight = 12f;
            head.Add(icon);
            var titles = Ui.Column();
            titles.style.flexGrow = 1f;
            titles.style.flexShrink = 1f;
            titles.Add(Ui.Text(CharacterLocalization.Title(kind), 22f, Theme.Text, true));
            var ability = Caption(CharacterLocalization.Ability(kind, new GameConfig()), 12f, Theme.TextDim, 0f, 2f);
            ability.style.whiteSpace = WhiteSpace.Normal;
            titles.Add(ability);
            head.Add(titles);
            foreach (var (value, label) in new[] { (progress.games, LocalizationService.Get("mastery.stat.games")), (progress.wins, LocalizationService.Get("mastery.stat.wins")), (progress.metric, metric.label) })
            {
                var stat = Ui.Column();
                stat.style.alignItems = Align.Center;
                stat.style.marginLeft = 18f;
                stat.Add(Ui.Text(value.ToString(), 22f, accent, true));
                stat.Add(Ui.Text(label, 11f, Theme.TextDim));
                head.Add(stat);
            }
            card.Add(head);

            // Древо: линии под карточками, карточки — по колонкам и рядам.
            var nodes = CharacterMastery.Tree(kind);
            var area = new VisualElement();
            area.style.width = ColumnStep * 3f + NodeWidth;
            area.style.height = RowStep * 2f + NodeHeight;
            area.style.alignSelf = Align.Center;
            area.style.marginTop = 18f;
            area.style.marginBottom = 4f;
            Vector2 TopLeft(MasteryNode n) => new Vector2(n.Column * ColumnStep, (n.Row + 1) * RowStep);
            var lines = new TreeLines();
            foreach (var node in nodes)
                foreach (var required in node.Requires)
                {
                    var from = nodes.First(n => n.Id == required);
                    var a = TopLeft(from) + new Vector2(NodeWidth, NodeHeight / 2f);
                    var b = TopLeft(node) + new Vector2(0f, NodeHeight / 2f);
                    bool lit = CharacterMastery.Done(profile, from);
                    lines.Links.Add((a, b, lit ? accent : new Color(1f, 1f, 1f, 0.16f)));
                }
            area.Add(lines);
            foreach (var node in nodes)
            {
                var box = MasteryNodeCard(profile, node, progress, accent);
                var at = TopLeft(node);
                box.style.position = Position.Absolute;
                box.style.left = at.x;
                box.style.top = at.y;
                area.Add(box);
            }
            card.Add(area);
            return card;
        }

        VisualElement MasteryNodeCard(PlayerProfile profile, MasteryNode node, CharacterProgress progress, Color accent)
        {
            bool done = CharacterMastery.Done(profile, node);
            bool open = CharacterMastery.Available(profile, node);
            int current = Mathf.Min(node.Current(progress), node.Target);
            var box = Ui.Column();
            box.style.width = NodeWidth;
            box.style.height = NodeHeight;
            box.style.backgroundColor = done ? new Color(accent.r * 0.28f, accent.g * 0.28f, accent.b * 0.28f, 0.98f) : GameColors.Button;
            Ui.Radius(box, 12f);
            Ui.Border(box, done ? 2f : 1f, done ? accent : open ? new Color(1f, 1f, 1f, 0.35f) : Theme.Line);
            Ui.Padding(box, 9f, 11f);
            if (!open && !done) box.style.opacity = 0.55f;

            var top = Ui.Row();
            top.style.alignItems = Align.Center;
            var title = Ui.Text(node.Title, 15f, done ? accent : Theme.Text, true);
            title.style.flexGrow = 1f;
            top.Add(title);
            top.Add(Ui.Text(LocalizationService.Get(done ? "mastery.status.done" : open ? "mastery.status.progress" : "mastery.status.locked"), 11f, done ? GameColors.Accent : Theme.TextDim, done));
            box.Add(top);
            var goal = Ui.Text(node.Goal, 12f, Theme.TextDim);
            goal.style.whiteSpace = WhiteSpace.Normal;
            goal.style.marginTop = 3f;
            goal.style.flexGrow = 1f;
            box.Add(goal);

            var barRow = Ui.Row();
            barRow.style.alignItems = Align.Center;
            var bar = Ui.Box();
            bar.style.flexGrow = 1f;
            bar.style.height = 6f;
            bar.style.backgroundColor = GameColors.Background;
            Ui.Radius(bar, 3f);
            var fill = Ui.Box();
            fill.style.height = 6f;
            fill.style.width = Length.Percent(done ? 100f : 100f * current / Mathf.Max(1, node.Target));
            fill.style.backgroundColor = accent;
            Ui.Radius(fill, 3f);
            bar.Add(fill);
            barRow.Add(bar);
            var numbers = Ui.Text(done ? $"{node.Target}/{node.Target}" : $"{current}/{node.Target}", 11f, Theme.TextDim, true);
            numbers.style.marginLeft = 6f;
            barRow.Add(numbers);
            box.Add(barRow);
            var reward = Ui.Text(string.Format(LocalizationService.Get("mastery.reward.label"), CharacterMastery.RewardText(node)), 11f, done ? GameColors.Warning : Theme.Text, done);
            reward.style.marginTop = 4f;
            reward.style.whiteSpace = WhiteSpace.Normal;
            box.Add(reward);
            if (!open && !done) box.tooltip = LocalizationService.Get("mastery.require_previous");
            return box;
        }

        /// <summary>Линии древа: плавные кривые от правого края одного узла к левому краю следующего.</summary>
        sealed class TreeLines : VisualElement
        {
            public readonly List<(Vector2 a, Vector2 b, Color color)> Links = new List<(Vector2, Vector2, Color)>();

            public TreeLines()
            {
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.top = 0f;
                style.right = 0f;
                style.bottom = 0f;
                generateVisualContent += context =>
                {
                    var p = context.painter2D;
                    p.lineWidth = 3f;
                    p.lineCap = LineCap.Round;
                    foreach (var (a, b, color) in Links)
                    {
                        p.strokeColor = color;
                        p.BeginPath();
                        p.MoveTo(a);
                        float mid = (a.x + b.x) / 2f;
                        p.BezierCurveTo(new Vector2(mid, a.y), new Vector2(mid, b.y), b);
                        p.Stroke();
                    }
                };
            }
        }
    }
}
