using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;
// События ссылок в тексте (<link>): PointerOverLinkTagEvent и др. — пока в Experimental.
using UnityEngine.UIElements.Experimental;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Правила»: слева — дерево разделов с бегущим индикатором активного пункта, сверху — поиск,
    /// справа — текст, где ключевые термины подсвечены (наведение — всплывающее определение, клик — глоссарий),
    /// инфографика вместо сплошного текста (схема хода, переворачивающиеся карты ресурсов, анимация магистрали)
    /// и кнопки «Попробовать», открывающие песочницы механик поверх страницы.
    /// </summary>
    public static class MenuRulesPage
    {
        public static VisualElement Build(GameConfig config, Action back) => new RulesView(config, back).Root;
        /// <summary>Страница с доступом к переключению режима (из «Обучения» открываем правила нужного режима).</summary>
        internal static RulesView Create(GameConfig config, Action back) => new RulesView(config, back);
    }

    /// <summary>
    /// Правила делятся на три режима — вкладки сверху: «Логистический магнат» (MenuRulesMagnate.cs),
    /// «Соревнование» и «Кооператив». У каждого режима своё дерево разделов, поиск ищет внутри режима.
    /// </summary>
    sealed partial class RulesView
    {
        sealed class SectionInfo
        {
            public string Group;
            public string Title;
            public MenuIcon.Glyph Glyph;
            public Func<VisualElement> Build;
            public VisualElement NavItem;
            public Label NavLabel;
            public MenuIcon NavIcon;
        }

        sealed class GroupInfo
        {
            public string Title;
            public MenuIcon.Glyph Glyph;
            public VisualElement Children;
            public MenuIcon Chevron;
            public bool Open = true;
        }

        readonly GameConfig _config;
        readonly List<SectionInfo> _sections = new List<SectionInfo>();
        readonly List<GroupInfo> _groups = new List<GroupInfo>();
        readonly VisualElement _nav;
        readonly VisualElement _indicator;
        readonly ScrollView _content;
        readonly VisualElement _popup;
        readonly MenuIcon _popupIcon;
        readonly VisualElement _popupCard;
        readonly Label _popupTitle;
        readonly Label _popupText;
        readonly Label _popupSection;
        readonly TextField _search;
        readonly Dictionary<string, VisualElement> _termCards = new Dictionary<string, VisualElement>();
        HashSet<string> _used = new HashSet<string>();
        List<(int section, string text)> _index;
        VisualElement _overlay;
        int _current = -1;
        readonly VisualElement _modeTabs;
        GameMode _mode = GameMode.Magnate;

        static readonly Color Own = Palette.PlayerColor(0);
        static readonly Color Rival = Palette.PlayerColor(1);
        static readonly Color Spot = new Color(0.85f, 0.86f, 0.90f);
        static readonly Regex Tags = new Regex("<[^>]+>");

        public VisualElement Root { get; }

        public RulesView(GameConfig config, Action back)
        {
            _config = config;
            Root = Ui.Column();

            var header = Ui.Row();
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 16f;
            var backButton = Ui.Button(LocalizationService.Get("menu.rules.001"), back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            header.Add(backButton);
            var title = Ui.Text(LocalizationService.Get("menu.rules.002"), 34f, Theme.Gold, true);
            title.style.flexGrow = 1f;
            header.Add(title);

            // Поиск: значок лупы и поле.
            var searchBox = Ui.Row();
            searchBox.style.alignItems = Align.Center;
            searchBox.style.width = 560f;
            searchBox.style.minHeight = 42f;
            searchBox.style.backgroundColor = Theme.PanelLight;
            Ui.Radius(searchBox, 10f);
            Ui.Border(searchBox, 1f, Theme.Line);
            Ui.Padding(searchBox, 2f, 10f);
            searchBox.Add(new MenuIcon(MenuIcon.Glyph.Search, 18f, Theme.TextDim));
            _search = new TextField { maxLength = 40 };
            _search.style.flexGrow = 1f;
            _search.style.fontSize = 15f;
            Ui.Margin(_search, 0f);
            _search.textEdition.placeholder = LocalizationService.Get("menu.rules.003");
            var input = _search.Q(className: TextField.inputUssClassName) ?? _search;
            input.style.backgroundColor = Color.clear;
            input.style.color = Theme.Text;
            Ui.Border(input, 0f, Color.clear);
            input.style.minHeight = 34f;
            // Текст и подсказка «Найти правило…» — по центру поля по высоте, с отступом от лупы.
            input.style.unityTextAlign = TextAnchor.MiddleLeft;
            input.style.paddingTop = 0f;
            input.style.paddingBottom = 0f;
            input.style.paddingLeft = 8f;
            _search.style.alignSelf = Align.Center;
            _search.RegisterValueChangedCallback(e => OnSearch(e.newValue));
            searchBox.Add(_search);
            header.Add(searchBox);
            Root.Add(header);

            _modeTabs = Ui.Column();
            Root.Add(_modeTabs);

            var body = Ui.Row();
            body.style.alignItems = Align.FlexStart;
            Root.Add(body);

            _nav = Ui.Column();
            _nav.style.width = 250f;
            _nav.style.marginRight = 16f;
            _nav.style.backgroundColor = Theme.Panel;
            Ui.Radius(_nav, 12f);
            Ui.Padding(_nav, 10f, 8f);
            body.Add(_nav);
            // Индикатор активного раздела — полоска слева, едет к выбранному пункту.
            _indicator = Ui.Box();
            _indicator.pickingMode = PickingMode.Ignore;
            _indicator.style.position = Position.Absolute;
            _indicator.style.left = 2f;
            _indicator.style.width = 4f;
            _indicator.style.height = 34f;
            _indicator.style.backgroundColor = Theme.Gold;
            Ui.Radius(_indicator, 2f);
            Ui.Transitions(_indicator, 0.25f, "top", "height", "opacity");

            _content = new ScrollView(ScrollViewMode.Vertical);
            _content.style.flexGrow = 1f;
            _content.style.height = 570f;
            Ui.StyleScroller(_content);
            _content.style.backgroundColor = Theme.Panel;
            Ui.Radius(_content, 12f);
            Ui.Padding(_content.contentContainer, 22f, 26f);
            body.Add(_content);

            // Всплывающее определение термина.
            _popup = Ui.Column();
            _popup.pickingMode = PickingMode.Ignore;
            _popup.style.position = Position.Absolute;
            _popup.style.width = 320f;
            _popup.style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(_popup, 12f);
            Ui.Padding(_popup, 12f, 14f);
            Ui.Shadow(_popup, 12f);
            Ui.Transitions(_popup, 0.15f, "opacity");
            var popupHead = Ui.Row();
            popupHead.style.alignItems = Align.Center;
            popupHead.pickingMode = PickingMode.Ignore;
            _popupIcon = new MenuIcon(MenuIcon.Glyph.Document, 24f, Theme.Gold);
            _popupIcon.style.marginRight = 8f;
            popupHead.Add(_popupIcon);
            _popupCard = Ui.Box();
            _popupCard.pickingMode = PickingMode.Ignore;
            popupHead.Add(_popupCard);
            _popupTitle = Ui.Text("", 17f, Theme.Gold, true);
            popupHead.Add(_popupTitle);
            _popup.Add(popupHead);
            _popupText = Ui.Text("", 13f, Theme.Text);
            _popupText.style.whiteSpace = WhiteSpace.Normal;
            _popupText.style.marginTop = 6f;
            _popup.Add(_popupText);
            _popupSection = Ui.Text("", 11f, Theme.TextDim);
            _popupSection.style.marginTop = 6f;
            _popup.Add(_popupSection);
            _popup.style.opacity = 0f;
            Ui.Show(_popup, false);
            Root.Add(_popup);

            _nav.RegisterCallback<GeometryChangedEvent>(_ => MoveIndicator());
            BuildNav();
        }

        /// <summary>Открыть правила нужного режима (вкладка сверху).</summary>
        public void SetMode(GameMode mode)
        {
            if (_mode == mode && _sections.Count > 0) return;
            _mode = mode;
            BuildNav();
        }

        /// <summary>Дерево разделов текущего режима.</summary>
        void BuildNav()
        {
            _modeTabs.Clear();
            _modeTabs.Add(ModeTabs.Build(_mode, SetMode));
            _search.SetValueWithoutNotify("");
            CloseSandbox();
            HideTerm();
            _nav.Clear();
            _sections.Clear();
            _groups.Clear();
            _index = null;
            _current = -1;
            switch (_mode)
            {
                case GameMode.Magnate:
                    AddMagnateSections();
                    break;
                case GameMode.Coop:
                    AddGroup(LocalizationService.Get("menu.rules.004"), MenuIcon.Glyph.Skyline);
                    AddSection(LocalizationService.Get("menu.rules.004"), LocalizationService.Get("menu.rules.005"), MenuIcon.Glyph.Skyline, Cooperative);
                    AddSection(LocalizationService.Get("menu.rules.004"), LocalizationService.Get("menu.rules.006"), MenuIcon.Glyph.Flag, Ending);
                    AddGroup(LocalizationService.Get("menu.rules.007"), MenuIcon.Glyph.Book);
                    AddSection(LocalizationService.Get("menu.rules.007"), LocalizationService.Get("menu.rules.008"), MenuIcon.Glyph.Play, Basics);
                    AddSection(LocalizationService.Get("menu.rules.007"), LocalizationService.Get("menu.rules.009"), MenuIcon.Glyph.Coins, ResourcesSection);
                    AddSection(LocalizationService.Get("menu.rules.007"), LocalizationService.Get("menu.rules.010"), MenuIcon.Glyph.Document, Contracts);
                    AddSection(LocalizationService.Get("menu.rules.007"), LocalizationService.Get("menu.rules.011"), MenuIcon.Glyph.Swap, Deals);
                    AddSection(LocalizationService.Get("menu.rules.007"), LocalizationService.Get("menu.rules.012"), MenuIcon.Glyph.Bell, Events);
                    AddSection(LocalizationService.Get("menu.rules.007"), LocalizationService.Get("menu.rules.013"), MenuIcon.Glyph.Document, Glossary);
                    break;
                default:
                    AddGroup(LocalizationService.Get("menu.rules.014"), MenuIcon.Glyph.Book);
                    AddSection(LocalizationService.Get("menu.rules.014"), LocalizationService.Get("menu.rules.008"), MenuIcon.Glyph.Play, Basics);
                    AddSection(LocalizationService.Get("menu.rules.014"), LocalizationService.Get("menu.rules.009"), MenuIcon.Glyph.Coins, ResourcesSection);
                    AddSection(LocalizationService.Get("menu.rules.014"), LocalizationService.Get("menu.rules.006"), MenuIcon.Glyph.Flag, Ending);
                    AddSection(LocalizationService.Get("menu.rules.014"), LocalizationService.Get("menu.rules.013"), MenuIcon.Glyph.Document, Glossary);
                    AddGroup(LocalizationService.Get("menu.rules.015"), MenuIcon.Glyph.Road);
                    AddSection(LocalizationService.Get("menu.rules.015"), LocalizationService.Get("menu.rules.010"), MenuIcon.Glyph.Document, Contracts);
                    AddSection(LocalizationService.Get("menu.rules.015"), LocalizationService.Get("menu.rules.011"), MenuIcon.Glyph.Swap, Deals);
                    AddSection(LocalizationService.Get("menu.rules.015"), LocalizationService.Get("menu.rules.012"), MenuIcon.Glyph.Bell, Events);
                    AddGroup(LocalizationService.Get("menu.rules.016"), MenuIcon.Glyph.Skyline);
                    AddSection(LocalizationService.Get("menu.rules.016"), LocalizationService.Get("menu.rules.017"), MenuIcon.Glyph.Star, GoalsAndLuck);
                    AddSection(LocalizationService.Get("menu.rules.016"), LocalizationService.Get("menu.rules.018"), MenuIcon.Glyph.Coins, InfluenceSection);
                    AddGroup(LocalizationService.Get("menu.rules.019"), MenuIcon.Glyph.Person);
                    AddSection(LocalizationService.Get("menu.rules.019"), LocalizationService.Get("menu.rules.020"), MenuIcon.Glyph.Person, CharactersSection);
                    AddSection(LocalizationService.Get("menu.rules.019"), LocalizationService.Get("menu.rules.021"), MenuIcon.Glyph.Globe, ScenariosSection);
                    break;
            }
            _nav.Add(_indicator);
            Select(0);
        }

        // ================================================================== Навигация

        void AddGroup(string title, MenuIcon.Glyph glyph)
        {
            var group = new GroupInfo { Title = title, Glyph = glyph };
            var head = Ui.Row();
            head.pickingMode = PickingMode.Position;
            head.style.alignItems = Align.Center;
            head.style.height = 34f;
            Ui.Padding(head, 0f, 8f);
            Ui.Radius(head, 8f);
            Ui.Transitions(head, "background-color");
            var icon = new MenuIcon(glyph, 18f, Theme.Gold);
            icon.style.marginRight = 8f;
            head.Add(icon);
            var label = Ui.Text(title.ToUpperInvariant(), 12f, Theme.TextDim, true);
            label.style.letterSpacing = 1.2f;
            label.style.flexGrow = 1f;
            head.Add(label);
            group.Chevron = new MenuIcon(MenuIcon.Glyph.Chevron, 14f, Theme.TextDim) { Level = 1 };
            head.Add(group.Chevron);
            head.RegisterCallback<PointerEnterEvent>(_ => head.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f));
            head.RegisterCallback<PointerLeaveEvent>(_ => head.style.backgroundColor = Color.clear);
            head.RegisterCallback<ClickEvent>(_ =>
            {
                // Аккордеон: группа сворачивается и разворачивается.
                group.Open = !group.Open;
                group.Chevron.Level = group.Open ? 1 : 0;
                Ui.Show(group.Children, group.Open);
                Ui.ButtonClicked?.Invoke();
                _nav.schedule.Execute(MoveIndicator).ExecuteLater(20);
            });
            _nav.Add(head);
            group.Children = Ui.Column();
            group.Children.style.marginBottom = 6f;
            _nav.Add(group.Children);
            _groups.Add(group);
        }

        void AddSection(string group, string title, MenuIcon.Glyph glyph, Func<VisualElement> build)
        {
            var section = new SectionInfo { Group = group, Title = title, Glyph = glyph, Build = build };
            int index = _sections.Count;
            var item = Ui.Row();
            item.pickingMode = PickingMode.Position;
            item.style.alignItems = Align.Center;
            item.style.height = 34f;
            item.style.marginLeft = 10f;
            Ui.Padding(item, 0f, 10f);
            Ui.Radius(item, 8f);
            Ui.Transitions(item, "background-color", "translate");
            section.NavIcon = new MenuIcon(glyph, 16f, Theme.TextDim);
            section.NavIcon.style.marginRight = 8f;
            item.Add(section.NavIcon);
            section.NavLabel = Ui.Text(title, 14f, Theme.Text, false);
            item.Add(section.NavLabel);
            item.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (_current != index) item.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
                item.style.translate = new Translate(new Length(3f), new Length(0f));
                Ui.Hovered?.Invoke();
            });
            item.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (_current != index) item.style.backgroundColor = Color.clear;
                item.style.translate = new Translate(new Length(0f), new Length(0f));
            });
            item.RegisterCallback<ClickEvent>(_ =>
            {
                Ui.ButtonClicked?.Invoke();
                _search.SetValueWithoutNotify("");
                Select(index);
            });
            section.NavItem = item;
            _groups.Last().Children.Add(item);
            _sections.Add(section);
        }

        void Select(int index, string highlight = null, string termId = null)
        {
            _current = index;
            HideTerm();
            for (int i = 0; i < _sections.Count; i++)
            {
                bool active = i == index;
                var s = _sections[i];
                s.NavItem.style.backgroundColor = active ? new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.16f) : Color.clear;
                s.NavLabel.style.color = active ? Theme.Gold : Theme.Text;
                s.NavLabel.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
                s.NavIcon.SetColor(active ? Theme.Gold : Theme.TextDim);
            }
            // Раздел мог быть в свёрнутой группе — раскроем её.
            var group = _groups.FirstOrDefault(g => g.Title == _sections[index].Group);
            if (group != null && !group.Open)
            {
                group.Open = true;
                group.Chevron.Level = 1;
                Ui.Show(group.Children, true);
            }
            _content.Clear();
            _termCards.Clear();
            _used = new HashSet<string>();
            var element = _sections[index].Build();
            _content.Add(element);
            _content.scrollOffset = Vector2.zero;
            Ui.Appear(element);
            _nav.schedule.Execute(MoveIndicator).ExecuteLater(20);
            if (highlight != null || termId != null)
                element.schedule.Execute(() => Spotlight(element, highlight, termId)).ExecuteLater(80);
        }

        void MoveIndicator()
        {
            if (_current < 0) return;
            var item = _sections[_current].NavItem;
            bool visible = item.resolvedStyle.display != DisplayStyle.None && item.parent.resolvedStyle.display != DisplayStyle.None;
            _indicator.style.opacity = visible ? 1f : 0f;
            if (!visible || float.IsNaN(item.worldBound.y)) return;
            _indicator.style.top = item.worldBound.y - _nav.worldBound.y + 4f;
            _indicator.style.height = item.worldBound.height - 8f;
        }

        /// <summary>Прокрутить к найденному абзацу (или карточке термина) и коротко подсветить его.</summary>
        void Spotlight(VisualElement root, string text, string termId)
        {
            VisualElement target = null;
            if (termId != null) _termCards.TryGetValue(termId, out target);
            if (target == null && !string.IsNullOrEmpty(text))
                target = root.Query<Label>().ToList().FirstOrDefault(l => Plain(l.text).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
            if (target == null) return;
            _content.ScrollTo(target);
            var old = target.resolvedStyle.backgroundColor;
            Ui.Transitions(target, 0.6f, "background-color");
            target.style.backgroundColor = new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.30f);
            target.schedule.Execute(() => target.style.backgroundColor = old).ExecuteLater(900);
        }

        // ================================================================== Поиск

        static string Plain(string text) => Tags.Replace(text ?? "", "");

        void BuildIndex()
        {
            _index = new List<(int, string)>();
            for (int i = 0; i < _sections.Count; i++)
            {
                _used = new HashSet<string>();
                VisualElement element;
                try { element = _sections[i].Build(); }
                catch (Exception e)
                {
                    Debug.LogWarning("Правила: раздел «" + _sections[i].Title + "» не собрался для поиска: " + e.Message);
                    continue;
                }
                _index.Add((i, _sections[i].Title));
                foreach (var label in element.Query<Label>().ToList())
                {
                    string plain = Plain(label.text).Trim();
                    if (plain.Length > 2) _index.Add((i, plain));
                }
            }
            _termCards.Clear();
        }

        void OnSearch(string query)
        {
            query = (query ?? "").Trim();
            if (query.Length < 2)
            {
                if (_current >= 0) Select(_current);
                return;
            }
            if (_index == null) BuildIndex();
            HideTerm();
            var results = _index.Where(x => x.text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(x => (x.section, x.text)).Select(g => g.First()).Take(30).ToList();

            var list = Ui.Column();
            list.Add(Ui.Text($"Поиск: «{query}»", 22f, Theme.Gold, true));
            list.Add(Caption(results.Count == 0 ? "Ничего не нашлось. Попробуйте другое слово: «мост», «проезд», «взятка»…"
                : $"Найдено: {results.Count}{(results.Count == 30 ? "+" : "")}. Нажмите на строку — откроется раздел.", 13f, Theme.TextDim, 4f));
            foreach (var (section, text) in results)
            {
                var row = Ui.Column();
                row.pickingMode = PickingMode.Position;
                row.style.marginTop = 8f;
                row.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(row, 10f);
                Ui.Padding(row, 8f, 12f);
                row.style.borderLeftWidth = 4f;
                row.style.borderLeftColor = Theme.Gold;
                Ui.Transitions(row, "background-color", "translate");
                var head = Ui.Row();
                head.pickingMode = PickingMode.Ignore;
                var icon = new MenuIcon(_sections[section].Glyph, 14f, Theme.Gold);
                icon.style.marginRight = 6f;
                head.Add(icon);
                head.Add(Ui.Text(_sections[section].Title, 12f, Theme.Gold, true));
                row.Add(head);
                var snippet = Ui.Text(Snippet(text, query), 13f, Theme.Text);
                snippet.pickingMode = PickingMode.Ignore;
                snippet.style.whiteSpace = WhiteSpace.Normal;
                snippet.style.marginTop = 2f;
                row.Add(snippet);
                row.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    row.style.backgroundColor = Theme.PanelHover;
                    row.style.translate = new Translate(new Length(3f), new Length(0f));
                    Ui.Hovered?.Invoke();
                });
                row.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    row.style.backgroundColor = Theme.PanelLight;
                    row.style.translate = new Translate(new Length(0f), new Length(0f));
                });
                int target = section;
                string found = text;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    Ui.ButtonClicked?.Invoke();
                    _search.SetValueWithoutNotify("");
                    Select(target, found.Length > 60 ? query : found);
                });
                list.Add(row);
            }
            _content.Clear();
            _content.Add(list);
            _content.scrollOffset = Vector2.zero;
        }

        /// <summary>Отрывок вокруг найденного слова; само слово — жирным золотым.</summary>
        static string Snippet(string text, string query)
        {
            int at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return text.Length > 160 ? text.Substring(0, 160) + "…" : text;
            int start = Mathf.Max(0, at - 70);
            int end = Mathf.Min(text.Length, at + query.Length + 90);
            string before = (start > 0 ? "…" : "") + text.Substring(start, at - start);
            string match = text.Substring(at, query.Length);
            string after = text.Substring(at + query.Length, end - at - query.Length) + (end < text.Length ? "…" : "");
            return $"{before}<b><color=#F6C66B>{match}</color></b>{after}";
        }

        // ================================================================== Термины

        /// <summary>Текст с подсвеченными терминами: наведение — определение, клик — карточка в глоссарии.</summary>
        Label Rich(string text, float size, Color color)
        {
            var label = Ui.Text(RulesGlossary.Linkify(_config, text, _used), size, color);
            label.enableRichText = true;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.RegisterCallback<PointerOverLinkTagEvent>(e => ShowTerm(e.linkID, e.position));
            label.RegisterCallback<PointerMoveLinkTagEvent>(e => PlacePopup(e.position));
            label.RegisterCallback<PointerOutLinkTagEvent>(_ => HideTerm());
            label.RegisterCallback<PointerDownLinkTagEvent>(e => OpenTerm(e.linkID));
            return label;
        }

        void ShowTerm(string id, Vector3 position)
        {
            var term = RulesGlossary.Find(_config, id);
            if (term == null) return;
            _popupTitle.text = term.Title;
            _popupTitle.style.color = term.Color;
            _popupText.text = term.Definition;
            _popupSection.text = $"Подробнее — раздел «{term.Section}». Нажмите на слово, чтобы открыть глоссарий.";
            _popupCard.Clear();
            if (term.Card.HasValue)
            {
                Ui.Show(_popupIcon, false);
                var card = new CardView(26f, 36f, false);
                card.Set(term.Card.Value);
                card.style.marginRight = 8f;
                card.pickingMode = PickingMode.Ignore;
                _popupCard.Add(card);
            }
            else
            {
                Ui.Show(_popupIcon, true);
                _popupIcon.SetGlyph(term.Glyph);
                _popupIcon.SetColor(term.Color);
            }
            Ui.Border(_popup, 1f, new Color(term.Color.r, term.Color.g, term.Color.b, 0.7f));
            Ui.Show(_popup, true);
            _popup.style.opacity = 1f;
            _popup.BringToFront();
            PlacePopup(position);
            Ui.Hovered?.Invoke();
        }

        void PlacePopup(Vector3 world)
        {
            var local = Root.WorldToLocal(world);
            float width = Root.resolvedStyle.width;
            float x = local.x + 14f;
            if (!float.IsNaN(width) && x + 330f > width) x = local.x - 334f;
            _popup.style.left = Mathf.Max(0f, x);
            _popup.style.top = local.y + 18f;
        }

        void HideTerm()
        {
            _popup.style.opacity = 0f;
            Ui.Show(_popup, false);
        }

        void OpenTerm(string id)
        {
            int glossary = _sections.FindIndex(s => s.Title == LocalizationService.Get("menu.rules.013"));
            if (glossary < 0) return;
            Ui.ButtonClicked?.Invoke();
            Select(glossary, null, id);
        }

        // ================================================================== Песочница

        VisualElement TryButton(RuleSandbox.Kind kind, string caption)
        {
            var button = Ui.Button("", () => OpenSandbox(kind), ButtonKind.Primary, 14f);
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.alignSelf = Align.FlexStart;
            button.style.marginTop = 12f;
            var icon = new MenuIcon(MenuIcon.Glyph.Play, 16f, Color.white);
            icon.style.marginRight = 8f;
            button.Add(icon);
            var label = Ui.Text(caption, 14f, Color.white, true);
            label.pickingMode = PickingMode.Ignore;
            button.Add(label);
            return button;
        }

        void OpenSandbox(RuleSandbox.Kind kind)
        {
            CloseSandbox();
            HideTerm();
            _overlay = Ui.Box();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = -20f;
            _overlay.style.top = -20f;
            _overlay.style.right = -20f;
            _overlay.style.bottom = -20f;
            _overlay.style.backgroundColor = Theme.Shade;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            Ui.Radius(_overlay, 16f);
            _overlay.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target == _overlay) CloseSandbox();
            });

            var window = Ui.Card(20f);
            window.style.width = 720f;
            window.style.backgroundColor = Theme.Panel;
            Ui.Shadow(window, 16f);
            var head = Ui.Row();
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 10f;
            var icon = new MenuIcon(MenuIcon.Glyph.Play, 20f, Theme.Gold);
            icon.style.marginRight = 8f;
            head.Add(icon);
            var title = Ui.Text(RuleSandbox.Title(kind), 20f, Theme.Gold, true);
            title.style.flexGrow = 1f;
            head.Add(title);
            var close = Ui.Button(LocalizationService.Get("menu.rules.022"), CloseSandbox, ButtonKind.Ghost, 14f);
            head.Add(close);
            window.Add(head);
            window.Add(RuleSandbox.Build(kind, _config));
            var note = Ui.Text("Это песочница: числа — из правил, но партия не идёт и ничего не сохраняется.", 11f, Theme.TextDim);
            note.style.marginTop = 10f;
            window.Add(note);
            _overlay.Add(window);
            Root.Add(_overlay);
            Ui.Appear(window);
            _overlay.style.opacity = 0f;
            Ui.Transitions(_overlay, 0.2f, "opacity");
            _overlay.schedule.Execute(() => _overlay.style.opacity = 1f).ExecuteLater(16);
        }

        void CloseSandbox()
        {
            _overlay?.RemoveFromHierarchy();
            _overlay = null;
        }

        // ================================================================== Строительные блоки

        VisualElement Section(string title)
        {
            var column = Ui.Column();
            column.Add(Ui.Text(title, 24f, Theme.Gold, true));
            return column;
        }

        void Paragraph(VisualElement parent, string text)
        {
            var label = Rich(text, 15f, Theme.Text);
            label.style.marginTop = 10f;
            Ui.Radius(label, 6f);
            parent.Add(label);
        }

        static Label Caption(string text, float size, Color color, float marginTop)
        {
            var label = Ui.Text(text, size, color);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = marginTop;
            return label;
        }

        static void Picture(VisualElement parent, VisualElement picture, string caption = null)
        {
            var box = Ui.Column();
            box.style.alignItems = Align.Center;
            box.style.marginTop = 14f;
            box.style.marginBottom = 4f;
            box.Add(picture);
            if (caption != null)
            {
                var label = Ui.Text(caption, 12f, Theme.TextDim);
                label.style.marginTop = 6f;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.maxWidth = 640f;
                box.Add(label);
            }
            parent.Add(box);
        }

        /// <summary>Плитка «заголовок + текст» с цветной полосой слева — для событий, персонажей, очков.</summary>
        VisualElement Tile(string title, string text, Color color, VisualElement icon = null)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8f;
            row.style.backgroundColor = Theme.PanelLight;
            Ui.Radius(row, 8f);
            Ui.Padding(row, 10f, 12f);
            row.style.borderLeftWidth = 5f;
            row.style.borderLeftColor = color;
            if (icon != null)
            {
                icon.style.marginRight = 12f;
                row.Add(icon);
            }
            var texts = Ui.Column();
            texts.style.flexShrink = 1f;
            texts.Add(Ui.Text(title, 15f, color, true));
            texts.Add(Rich(text, 13f, Theme.Text));
            row.Add(texts);
            return row;
        }

        // ================================================================== Разделы

        VisualElement Basics()
        {
            var s = Section(LocalizationService.Get("menu.rules.008"));
            Paragraph(s, "Вы — дорожная компания. Стройте дороги между местами города, выполняйте контракты мэрии на связь районов и берите плату, когда соперники ездят по вашим дорогам. Побеждает тот, у кого больше очков в конце партии.");

            // Пошаговая схема хода: 1 → 2 → 3 → 4 и обратно к следующему игроку.
            var flow = Ui.Row();
            flow.style.marginTop = 16f;
            flow.style.alignItems = Align.Stretch;
            var steps = new[]
            {
                (LocalizationService.Get("menu.rules.023"), MenuIcon.Glyph.Bell, new[] { $"Событие города с шансом {Mathf.RoundToInt(_config.EventChance * 100)}%", "Иногда — аукцион мэрии", "Отстающим — немного влияния" }),
                (LocalizationService.Get("menu.rules.024"), MenuIcon.Glyph.Swap, new[] { "Сделка с соперником", "Трата влияния", "Взятка (тайно)", "Всё по желанию, ход не тратит" }),
                (LocalizationService.Get("menu.rules.025"), MenuIcon.Glyph.Play, new[] { LocalizationService.Get("menu.rules.026"), LocalizationService.Get("menu.rules.027"), $"Взять {_config.ContractsDrawn} контракта", LocalizationService.Get("menu.rules.028") }),
                (LocalizationService.Get("menu.rules.029"), MenuIcon.Glyph.Flag, new[] { LocalizationService.Get("menu.rules.030"), LocalizationService.Get("menu.rules.031"), LocalizationService.Get("menu.rules.032") })
            };
            for (int i = 0; i < steps.Length; i++)
            {
                var (title, glyph, lines) = steps[i];
                var card = Ui.Column();
                card.style.width = 172f;
                card.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(card, 12f);
                Ui.Padding(card, 12f, 12f);
                card.style.borderTopWidth = 4f;
                card.style.borderTopColor = i == 2 ? Theme.Gold : new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.45f);
                var head = Ui.Row();
                head.style.alignItems = Align.Center;
                var number = Ui.Number((i + 1).ToString(), 16f, Theme.GoldText, true);
                number.style.width = 28f;
                number.style.height = 28f;
                number.style.unityTextAlign = TextAnchor.MiddleCenter;
                number.style.backgroundColor = Theme.Gold;
                Ui.Radius(number, 14f);
                head.Add(number);
                var icon = new MenuIcon(glyph, 18f, Theme.Gold);
                icon.style.marginLeft = 8f;
                head.Add(icon);
                card.Add(head);
                card.Add(Ui.Text(title, 15f, Theme.Text, true));
                foreach (var line in lines)
                {
                    var item = Ui.Row();
                    item.style.marginTop = 4f;
                    var dot = Ui.Box();
                    dot.style.width = 5f;
                    dot.style.height = 5f;
                    dot.style.marginTop = 6f;
                    dot.style.marginRight = 6f;
                    dot.style.backgroundColor = Theme.Gold;
                    Ui.Radius(dot, 3f);
                    item.Add(dot);
                    var text = Ui.Text(line, 12f, Theme.TextDim);
                    text.style.whiteSpace = WhiteSpace.Normal;
                    text.style.flexShrink = 1f;
                    item.Add(text);
                    card.Add(item);
                }
                flow.Add(card);
                if (i < steps.Length - 1)
                {
                    var arrow = new FlowArrow(28f, 40f, Theme.Gold);
                    arrow.style.alignSelf = Align.Center;
                    flow.Add(arrow);
                }
            }
            s.Add(flow);
            var loop = Ui.Row();
            loop.style.alignItems = Align.Center;
            loop.style.justifyContent = Justify.Center;
            loop.style.marginTop = 8f;
            var loopIcon = new MenuIcon(MenuIcon.Glyph.Person, 16f, Theme.TextDim);
            loopIcon.style.marginRight = 6f;
            loop.Add(loopIcon);
            loop.Add(Ui.Text("…и ход переходит к следующему игроку. Когда все сходили — новый круг.", 13f, Theme.TextDim));
            s.Add(loop);

            Paragraph(s, "Главное действие — одно из трёх: взять карты ресурсов (2 из открытого ряда или колоды, одну можно заменить картой удачи), построить дорогу (сдать карты её ресурса по длине) или взять контракты и оставить хотя бы один. Вместо стройки можно улучшить свою дорогу до магистрали.");

            var points = Ui.Row();
            points.style.marginTop = 14f;
            points.style.justifyContent = Justify.Center;
            for (int length = 1; length <= _config.MaxRoadLength; length++)
            {
                var cell = Ui.Column();
                cell.style.alignItems = Align.Center;
                cell.style.marginRight = 10f;
                var bar = Ui.Box();
                bar.style.width = 14f * length + 6f;
                bar.style.height = 12f;
                bar.style.backgroundColor = Own;
                Ui.Radius(bar, 3f);
                cell.Add(bar);
                cell.Add(Ui.Text($"длина {length}", 11f, Theme.TextDim));
                cell.Add(Ui.Text($"+{_config.PointsForLength(length)}", 16f, Theme.Gold, true));
                points.Add(cell);
            }
            Picture(s, points, "Очки за построенную дорогу: длинные дороги выгоднее.");
            return s;
        }

        VisualElement ResourcesSection()
        {
            var s = Section(LocalizationService.Get("menu.rules.009"));
            Paragraph(s, "Цвет дороги на карте — ресурс, которым её строят. Дорога длины 3 стоит 3 карты своего ресурса; белые дороги — любым одним ресурсом. Нажмите на карту, чтобы перевернуть её.");

            // Сколько дорог каждого ресурса на карте Москвы — для оборота карт.
            var onMap = new Dictionary<Resource, int>();
            int anyRoads = 0;
            try
            {
                var map = CityHighwaysApp.LoadMap("CityHighways/Maps/moscow");
                foreach (var road in map.Roads)
                {
                    if (road.Resource.HasValue) onMap[road.Resource.Value] = (onMap.TryGetValue(road.Resource.Value, out var n) ? n : 0) + 1;
                    else anyRoads++;
                }
            }
            catch
            {
                // Нет карты — оборот покажет только колоду.
            }

            var row = Ui.Row();
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.marginTop = 14f;
            var info = new[]
            {
                (Resource.Asphalt, LocalizationService.Get("menu.rules.033"), "Городские улицы — самые частые дороги."),
                (Resource.Concrete, LocalizationService.Get("menu.rules.034"), "Проспекты и кольца — длинные и выгодные."),
                (Resource.Steel, LocalizationService.Get("menu.rules.035"), $"Мосты через реку. «Субсидия мэрии» делает их дешевле на {_config.BridgeSubsidy} карту."),
                (Resource.Gravel, LocalizationService.Get("menu.rules.036"), $"Дороги за городом. В «Снегопад» дороже на {_config.SnowExtraCost} карту."),
                (Resource.Machinery, LocalizationService.Get("menu.rules.037"), "Заменяет любую карту. Открытую технику первой картой — весь ход, второй — нельзя.")
            };
            foreach (var (resource, role, use) in info)
            {
                var color = Palette.Card(resource);
                var front = Ui.Column();
                front.style.alignItems = Align.Center;
                front.style.justifyContent = Justify.Center;
                front.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(front, 12f);
                Ui.Border(front, 2f, new Color(color.r, color.g, color.b, 0.8f));
                var card = new CardView(78f, 108f, false);
                card.Set(resource);
                card.pickingMode = PickingMode.Ignore;
                front.Add(card);
                var name = Ui.Text(Palette.CardTitle(resource), 15f, Theme.Text, true);
                name.style.marginTop = 8f;
                front.Add(name);
                front.Add(Ui.Text(role, 12f, Theme.TextDim));
                var flipHint = Ui.Text("нажмите", 10f, new Color(1f, 1f, 1f, 0.35f));
                flipHint.style.marginTop = 4f;
                front.Add(flipHint);

                var back = Ui.Column();
                back.style.backgroundColor = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 1f);
                Ui.Radius(back, 12f);
                Ui.Border(back, 2f, color);
                Ui.Padding(back, 12f, 10f);
                back.Add(Ui.Text(Palette.CardTitle(resource), 15f, Color.white, true));
                int inDeck = resource == Resource.Machinery ? _config.MachineryCards
                    : (int)resource < _config.CardsPerResource.Length ? _config.CardsPerResource[(int)resource] : 0;
                back.Add(Ui.Number($"{inDeck}", 30f, Color.white, true));
                back.Add(Ui.Text("карт в колоде", 11f, new Color(1f, 1f, 1f, 0.7f)));
                if (resource != Resource.Machinery && onMap.TryGetValue(resource, out var count))
                    back.Add(Caption($"На карте Москвы: {count} дорог", 11f, Color.white, 6f));
                else if (resource == Resource.Machinery && anyRoads > 0)
                    back.Add(Caption($"Белых дорог: {anyRoads}", 11f, Color.white, 6f));
                back.Add(Caption(use, 11f, new Color(1f, 1f, 1f, 0.85f), 6f));
                row.Add(new FlipCard(150f, 230f, front, back));
            }
            s.Add(row);
            Paragraph(s, $"В открытом ряду {_config.FaceUpSlots} карт. Если в нём больше {_config.MaxFaceUpMachinery} техники, ряд обновляется.");
            return s;
        }

        VisualElement Contracts()
        {
            var s = Section(LocalizationService.Get("menu.rules.010"));
            Paragraph(s, "Контракт — соединить два района, подойдёт любое их место. Выполненный приносит очки, невыполненный в конце партии их отнимает.");
            Paragraph(s, $"Чужая дорога — не тупик. Контракт засчитается, если на пути есть хотя бы одна ваша дорога и не больше {_config.MaxForeignRoadsPerContract} чужих. За каждую чужую дорогу вы платите её владельцу {_config.TollPerRoad} очка.");
            Paragraph(s, "Проезд платится сразу, как только контракт выполнен: в этот момент выбирается самый дешёвый путь, и он закрепляется. Если владелец потом улучшит дорогу, ваш контракт не подорожает — успейте раньше. А своя дорога в обход, построенная позже, уже не вернёт заплаченное.");

            var diagram = new RuleDiagram(640f, 170f)
                .Place("a", 0.08f, 0.55f, MapView.FromColor, "Район А")
                .Place("x", 0.36f, 0.30f, Spot, null)
                .Place("y", 0.64f, 0.30f, Spot, null)
                .Place("b", 0.92f, 0.55f, MapView.ToColor, "Район Б")
                .Road("a", "x", Own, "ваша")
                .Road("x", "y", Rival, $"чужая −{_config.TollPerRoad}")
                .Road("y", "b", Rival, $"чужая −{_config.TollPerRoad}");
            Picture(s, diagram, $"Контракт А — Б выполнен: одна своя дорога и две чужие. Владелец чужих получит {2 * _config.TollPerRoad} очка.");
            s.Add(TryButton(RuleSandbox.Kind.Toll, "Попробовать: соберите путь контракта"));

            Paragraph(s, "Сложные контракты приносят больше очков, но и провал стоит дороже:");
            var transit = new RuleDiagram(310f, 150f)
                .Place("a", 0.10f, 0.50f, MapView.FromColor, "А")
                .Place("c", 0.50f, 0.25f, MapView.ThirdColor, "через В")
                .Place("b", 0.90f, 0.50f, MapView.ToColor, "Б")
                .Road("a", "c", Own)
                .Road("c", "b", Own);
            var hub = new RuleDiagram(310f, 150f)
                .Place("a", 0.50f, 0.30f, MapView.FromColor, "А")
                .Place("b", 0.12f, 0.70f, MapView.ToColor, "Б")
                .Place("c", 0.88f, 0.70f, MapView.ThirdColor, "В")
                .Road("a", "b", Own)
                .Road("a", "c", Own);
            var pair = Ui.Row();
            pair.style.justifyContent = Justify.SpaceBetween;
            var left = Ui.Column();
            Picture(left, transit, "Транзит: один путь из А в Б через район В.");
            var right = Ui.Column();
            Picture(right, hub, "Узел: район А связан и с Б, и с В.");
            pair.Add(left);
            pair.Add(right);
            s.Add(pair);
            return s;
        }

        VisualElement Events()
        {
            var s = Section(LocalizationService.Get("menu.rules.038"));
            Paragraph(s, $"В начале каждого круга с шансом {Mathf.RoundToInt(_config.EventChance * 100)}% открывается событие и действует до конца круга; иначе круг спокойный. Карточка события видна во время игры.");
            void Add(CityEventKind kind, string title, string text) => s.Add(Tile(title, text, GameView.EventColor(kind), new MenuIcon(MenuIcon.Glyph.Bell, 26f, GameView.EventColor(kind))));
            Add(CityEventKind.RushHour, LocalizationService.Get("menu.rules.039"), $"Дороги, построенные в этот круг, становятся загруженными магистралями: проезд по ним — {_config.TollPerRoad * _config.RushHourTollMultiplier} очка.");
            Add(CityEventKind.Repair, LocalizationService.Get("menu.rules.040"), $"{_config.RepairRoads} случайных свободных участка закрыты для стройки.");
            Add(CityEventKind.Subsidy, LocalizationService.Get("menu.rules.041"), $"Мосты дешевле на {_config.BridgeSubsidy} карту стали.");
            Add(CityEventKind.NewDistrict, LocalizationService.Get("menu.rules.042"), "На окраине открываются новые места, дороги и контракты.");
            Add(CityEventKind.Parade, LocalizationService.Get("menu.rules.043"), "Дороги, касающиеся Центра, закрыты для стройки.");
            Add(CityEventKind.Snow, LocalizationService.Get("menu.rules.044"), $"Щебёночные дороги на {_config.SnowExtraCost} карту дороже.");
            Add(CityEventKind.Investor, LocalizationService.Get("menu.rules.045"), $"Каждая дорога, построенная в этот круг, приносит ещё +{_config.InvestorBonus} очка.");
            return s;
        }

        VisualElement GoalsAndLuck()
        {
            var s = Section(LocalizationService.Get("menu.rules.017"));
            Paragraph(s, $"В начале партии открываются {_config.MayorGoalsDealt} общие цели мэрии — например, «своя дорога в 2 аэропортах» или «3 моста». Награду получает тот, кто выполнит цель первым; остальным она уже не достанется.");
            Paragraph(s, "Когда берёте карты, одну из двух можно заменить картой удачи — не больше одной за ход. Под кнопкой «Удача» видно, сколько бафов и дебафов осталось в колоде.");

            var cards = Ui.Row();
            cards.style.flexWrap = Wrap.Wrap;
            cards.style.justifyContent = Justify.SpaceBetween;
            for (int k = 0; k < LuckInfo.KindCount; k++)
            {
                var kind = (LuckCardKind)k;
                bool buff = LuckInfo.IsBuff(kind);
                var card = Ui.Card(12f);
                card.style.width = 240f;
                card.style.minHeight = 120f;
                card.style.marginTop = 10f;
                card.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(card, 10f);
                Ui.Border(card, 2f, GameView.LuckColor(buff));
                card.Add(Ui.Text((buff ? LocalizationService.Get("menu.rules.046") : LocalizationService.Get("menu.rules.047")) + $" · {LuckInfo.CountIn(_config, kind)} шт.", 11f, GameView.LuckColor(buff), true));
                card.Add(Ui.Text(LuckInfo.Title(kind), 15f, Theme.Text, true));
                var text = Ui.Text(LuckInfo.Describe(kind, _config), 12f, Theme.TextDim);
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.marginTop = 4f;
                card.Add(text);
                cards.Add(card);
            }
            s.Add(cards);
            return s;
        }

        VisualElement Deals()
        {
            var s = Section(LocalizationService.Get("menu.rules.011"));
            Paragraph(s, $"Сделка: раз в ход, до основного действия, предложите одному сопернику обмен — от 1 до {_config.TradeMaxCards} карт одного ресурса на 1–{_config.TradeMaxCards} карты другого. Он соглашается или отказывается. Ход сделка не тратит.");
            s.Add(TryButton(RuleSandbox.Kind.Deal, "Попробовать: предложите сделку"));
            Paragraph(s, $"Магистраль: кликните по своей дороге — её можно улучшить за ход и половину её длины в картах её ресурса. Она приносит +{_config.UpgradePoints} очка, а проезд по ней для соперников становится в {_config.RushHourTollMultiplier} раза дороже.");
            Picture(s, new RoadBuildAnimation(640f, 130f, Mathf.Min(4, _config.MaxRoadLength), Own),
                "Как это выглядит: дорога строится участками, потом её улучшают до магистрали — с оранжевой рамкой.");
            var diagram = new RuleDiagram(640f, 150f)
                .Place("a", 0.10f, 0.55f, Spot, null)
                .Place("b", 0.50f, 0.55f, Spot, null)
                .Place("c", 0.90f, 0.55f, Spot, null)
                .Road("a", "b", Own, "обычная: проезд " + _config.TollPerRoad)
                .Road("b", "c", Own, "магистраль: проезд " + _config.TollPerRoad * _config.RushHourTollMultiplier, frame: true);
            Picture(s, diagram, "Магистраль на карте — с оранжевой рамкой. Так же выглядят дороги, построенные в час пик.");

            if (_config.JamThreshold > 0)
            {
                Paragraph(s, $"Пробки. Загрузка дороги — сколько выполненных контрактов соперников по ней едет (видно по машинкам и в подсказке). " +
                             $"Обычная дорога с загрузкой {_config.JamThreshold} и больше стоит в пробке — рамка мигает красным. Каждый следующий проезд по ней " +
                             $"стоит ещё +{_config.JamFee} очка, и они сгорают: владельцу не достаются. Соперникам выгодно построить объезд, " +
                             "а владельцу — улучшить дорогу: магистраль в пробку не встаёт, а проезд по ней дороже.");
            }
            s.Add(TryButton(RuleSandbox.Kind.Highway, "Попробовать: магистраль и пробка"));
            if (_config.PassesEnabled)
                Paragraph(s, "Пропуск. В сделке можно продать сопернику пропуск: до конца партии он ездит по всем вашим дорогам, не платя вам. " +
                             "Пропуск продаётся за карты, влияние или в обмен на его пропуск. Пробочный сбор городу пропуск не отменяет.");
            return s;
        }

        VisualElement InfluenceSection()
        {
            var s = Section(LocalizationService.Get("menu.rules.018"));
            Paragraph(s, "Влияние — особая валюта мэрии. В очки оно не превращается: его тратят во время партии, а остаток пригодится в профиле.");
            Paragraph(s, $"Откуда берётся: выполненный контракт +{_config.InfluencePerContract} (сложный +{_config.InfluencePerComplexContract}); дорога в новый для вашей сети район +{_config.InfluencePerNewDistrict} (кроме самой первой дороги); цель мэрии +{_config.InfluencePerGoal}; «Грант мэрии» из колоды удачи +{_config.LuckGrantInfluence}; в начале круга, если вы отстаёте от лидера на {_config.InfluenceCatchUpGap} очков и больше, +{_config.InfluenceCatchUp}.");
            Paragraph(s, "Тратить можно раз в ход, до основного действия; ход это не занимает:");
            foreach (var action in InfluenceInfo.All)
                s.Add(Tile($"{InfluenceInfo.Title(action)} · {InfluenceInfo.Cost(action, _config)} вл.",
                    InfluenceInfo.Describe(action, _config) +
                    (action == InfluenceAction.RefreshMarket && _config.InfluenceRefreshStep > 0 ? $" Каждое следующее ваше обновление на {_config.InfluenceRefreshStep} дороже." : ""),
                    new Color(0.75f, 0.60f, 0.98f), new MenuIcon(InfluenceInfo.IsCityAction(action) ? MenuIcon.Glyph.Skyline : MenuIcon.Glyph.Coins, 26f, new Color(0.75f, 0.60f, 0.98f))));
            s.Add(TryButton(RuleSandbox.Kind.Influence, "Попробовать: потратьте влияние"));
            Paragraph(s, $"В сделках влиянием можно доплатить или купить карты: до {_config.TradeMaxInfluence} влияния с каждой стороны.");
            Paragraph(s, $"Аукцион мэрии — раз за партию, в начале круга, когда кто-то построил треть своих участков. Мэрия выставляет самый дорогой из верхних контрактов колоды. Каждый делает одну тайную ставку влиянием; самая высокая забирает контракт и платит свою ставку (при равенстве — отстающий по очкам), остальные не платят. Выполнен к концу партии — +{_config.AuctionBonus} очков сверх обычного, не выполнен — штраф как у любого контракта. Ставок нет — контракт уходит под колоду.");
            Paragraph(s, $"Взятки — от {_config.BribesMinPlayers} игроков: раз в ход, тайно, за влияние — «Ускорить согласование» ({_config.BribeDiscountCost} вл.: в этот ход дороги на 1 карту дешевле) или «Внеочередная поставка» ({_config.BribeExtraCardCost} вл.: сразу карта из колоды). В момент взятки никто ничего не видит. В начале следующего круга объявляют только, сколько было взяток, — без имён. Раскрыть взятку можно до конца круга после неё: любой в свой ход обвиняет любого соперника. Угадал — давший платит штраф за каждую раскрытую взятку (на троих −{_config.BribeFineFor(3)}, на четверых и пятерых −{_config.BribeFineFor(4)}), а разоблачитель получает +{_config.BribeWhistleReward} влияния; ошибся — −{_config.FalseAccusationFine} обвинителю. Улики видны всем: рука за ход выросла на 3 карты и больше или дорога обошлась дешевле своей длины. Но так бывает и честно — после сделки, «Поставки», с субсидией или у Мостовика.");
            return s;
        }

        VisualElement ScenariosSection()
        {
            var s = Section(LocalizationService.Get("menu.rules.021"));
            Paragraph(s, "Сценарий выбирается в меню «Новая игра» (в сетевой — хостом в лобби) и меняет правила на всю партию. Название сценария — рядом с номером хода.");
            foreach (var kind in Scenarios.All)
            {
                if (kind == ScenarioKind.Standard) continue;
                var card = Ui.Card(14f);
                card.style.marginTop = 10f;
                card.style.backgroundColor = Theme.PanelLight;
                var head = Ui.Row();
                head.style.alignItems = Align.Center;
                var icon = new MenuIcon(MenuIcon.Glyph.Globe, 20f, Theme.Gold);
                icon.style.marginRight = 8f;
                head.Add(icon);
                head.Add(Ui.Text(Scenarios.Title(kind), 16f, Theme.Gold, true));
                card.Add(head);
                var summary = Rich(Scenarios.Summary(kind), 13f, Theme.Text);
                summary.style.marginTop = 4f;
                card.Add(summary);
                foreach (var change in Scenarios.Changes(kind))
                {
                    var line = Rich("• " + change, 12f, Theme.TextDim);
                    line.style.marginTop = 3f;
                    card.Add(line);
                }
                s.Add(card);
            }
            return s;
        }

        VisualElement CharactersSection()
        {
            var s = Section(LocalizationService.Get("menu.rules.020"));
            Paragraph(s, $"У каждой компании свой персонаж со способностью. По умолчанию — «выбор из двух»: в начале партии игрок видит {_config.CharacterOffer} случайных персонажей (по возможности не те, что у соперников) вместе со стартовыми контрактами и выбирает одного. Можно и задать персонажа заранее в меню или раздать случайно. В сетевой игре все выбирают из двух. Способность написана под именем игрока.");
            int i = 0;
            foreach (var kind in Characters.All)
            {
                s.Add(Tile(Characters.Title(kind), Characters.Ability(kind, _config) + " " + Characters.Hint(kind),
                    Palette.PlayerColor(i), new CharacterIcon(44f, kind, Palette.PlayerColor(i))));
                i++;
            }
            return s;
        }

        VisualElement Ending()
        {
            var s = Section(LocalizationService.Get("menu.rules.006"));
            Paragraph(s, $"Когда у кого-то остаётся {_config.FinalRoundPieces} участка дороги или меньше, начинается последний круг: каждый, включая этого игрока, делает ещё по одному ходу. Под статусом виден счётчик ходов до конца." +
                         (_config.FinalWarningPieces > _config.FinalRoundPieces ? $" Заранее, когда у кого-то остаётся {_config.FinalWarningPieces} участков или меньше, всем объявляют «скоро финал»." : ""));
            if (_config.FinalOrderBonus > 0)
                Paragraph(s, $"Финальный заказ мэрии: в начале последнего круга мэрия выбирает одну свободную дорогу — ту, за которую могут побороться больше всего компаний. На карте она в золотой рамке. Кто построит её в последнем круге, получает +{_config.FinalOrderBonus} очков (в столбце «Цели»).");
            if (_config.BribesEnabled && _config.FinalAuditPercent > 0)
                Paragraph(s, $"Проверка перед итогами: каждая ещё нераскрытая взятка последних двух кругов раскрывается с шансом {_config.FinalAuditPercent}% — с тем же штрафом, что и при обвинении. Взятка в последнем круге — тоже риск.");
            Paragraph(s, "Итог складывается из:");
            var lines = new[]
            {
                ("Дороги", "очки за каждую построенную дорогу и магистрали", MenuIcon.Glyph.Road),
                ("Контракты", "выполненные — плюсом, невыполненные — минусом", MenuIcon.Glyph.Document),
                ("Проезд", "плата владельцам чужих дорог и плата вам за ваши; сборы в пробках сгорают", MenuIcon.Glyph.Coins),
                ("Крупнейшая сеть", $"+{_config.NetworkBonus} за связную сеть на больше всего районов", MenuIcon.Glyph.Pin),
                ("Цели мэрии", "награды за цели, выполненные первым", MenuIcon.Glyph.Trophy),
                ("Удача", "гранты, премии и штрафы карт удачи", MenuIcon.Glyph.Star),
                ("Персонаж", "Чиновник — за контракты, Мостовик — за мосты", MenuIcon.Glyph.Person)
            };
            foreach (var (title, text, glyph) in lines) s.Add(Tile(title, text, Theme.Gold, new MenuIcon(glyph, 24f, Theme.Gold)));
            return s;
        }

        VisualElement Cooperative()
        {
            var coop = new GameConfig().MakeCooperative();
            var s = Section("Кооператив «Город против всех»");
            Paragraph(s, "Режим выбирается в «Новой игре» (в сети — хостом в лобби). Все компании — одна команда, противник — сам город. Побеждают или проигрывают все вместе.");
            Paragraph(s, "Сеть общая: дорога любого союзника засчитывается в ваши контракты, и проезд по ней бесплатный. Взяток, аукциона и пропусков нет — делиться картами помогают сделки.");

            // Шкала кризиса — как на панели в партии.
            var meter = Ui.Row();
            meter.style.width = 420f;
            meter.style.height = 16f;
            for (int i = 0; i < coop.CoopCrisisMax; i++)
            {
                var cell = Ui.Box();
                cell.style.flexGrow = 1f;
                cell.style.marginRight = 3f;
                cell.style.backgroundColor = i < coop.CoopCrisisStart + 3 ? CoopPanel.CrisisColor(i / (float)(coop.CoopCrisisMax - 1)) : new Color(1f, 1f, 1f, 0.12f);
                Ui.Radius(cell, 3f);
                meter.Add(cell);
            }
            Picture(s, meter, $"Кризис: старт {coop.CoopCrisisStart}, предел {coop.CoopCrisisMax}. На картинке — {coop.CoopCrisisStart + 3} из {coop.CoopCrisisMax}.");

            Paragraph(s, $"На столе — заказы города: вдвоём и втроём по {coop.CoopOpenOrdersFor(2)}, вчетвером и впятером по {coop.CoopOpenOrdersFor(4)}. Это контракты, общие для всей команды. Заказ выполнен, когда общая сеть связывает его районы: очки получает тот, чья дорога замкнула путь, а кризис падает на {coop.CoopCrisisReliefPerOrder}. Просроченный заказ поднимает кризис на {coop.CoopCrisisPerFailedOrder}.");
            Paragraph(s, $"Срок заказа зависит от его длины и числа игроков: вдвоём на заказ в 10 очков — {coop.CoopOrderRoundsFor(10, 2)} кругов, вчетвером — {coop.CoopOrderRoundsFor(10, 4)} (ходов в круге больше). Стартовые заказы открываются в очередь: у каждого следующего срок на {coop.CoopStartStagger} круга больше, чтобы они не сгорели разом.");
            Paragraph(s, $"Город растёт сам: кризис +1 каждые {coop.CoopCrisisEveryRounds} круга, а ремонт, снегопад и парад добавляют ещё +{coop.CoopCrisisPerBadEvent}.");
            var outcomes = Ui.Row();
            outcomes.style.marginTop = 10f;
            outcomes.style.justifyContent = Justify.SpaceBetween;
            var win = Tile(LocalizationService.Get("menu.rules.048"), $"Выполнить заказы города: на двоих — {coop.CoopOrdersToWin(2)}, на троих — {coop.CoopOrdersToWin(3)}, на четверых — {coop.CoopOrdersToWin(4)}, на пятерых — {coop.CoopOrdersToWin(5)}.",
                GameColors.Success, new MenuIcon(MenuIcon.Glyph.Trophy, 26f, GameColors.Success));
            win.style.width = 380f;
            var lose = Tile(LocalizationService.Get("menu.rules.049"), $"Кризис дошёл до {coop.CoopCrisisMax} — или у кого-то кончились участки и последний круг прошёл, а заказов не хватило.",
                GameColors.Danger, new MenuIcon(MenuIcon.Glyph.Flame, 26f, GameColors.Danger));
            lose.style.width = 380f;
            outcomes.Add(win);
            outcomes.Add(lose);
            s.Add(outcomes);
            Paragraph(s, "Свои контракты тоже никуда не делись: выполненные дают очки, проваленные отнимают — это общий счёт команды. Боты-союзники видят заказы города и строят под них в первую очередь. Панель заказов — в правом верхнем углу поля; наведите на заказ, чтобы подсветить его районы.");
            return s;
        }

        VisualElement Glossary()
        {
            var s = Section(LocalizationService.Get("menu.rules.013"));
            var intro = Caption("Все термины правил коротко. В тексте разделов они подчёркнуты: наведите — появится определение, нажмите — откроется эта страница.", 14f, Theme.TextDim, 8f);
            s.Add(intro);
            var grid = Ui.Row();
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.SpaceBetween;
            grid.style.marginTop = 10f;
            foreach (var term in RulesGlossary.All(_config).OrderBy(t => t.Title))
            {
                var card = Ui.Row();
                card.style.width = 380f;
                card.style.marginBottom = 10f;
                card.style.backgroundColor = Theme.PanelLight;
                Ui.Radius(card, 10f);
                Ui.Padding(card, 10f, 12f);
                card.style.borderLeftWidth = 4f;
                card.style.borderLeftColor = term.Color;
                if (term.Card.HasValue)
                {
                    var view = new CardView(30f, 42f, false);
                    view.Set(term.Card.Value);
                    view.style.marginRight = 10f;
                    card.Add(view);
                }
                else
                {
                    var icon = new MenuIcon(term.Glyph, 28f, term.Color);
                    icon.style.marginRight = 10f;
                    card.Add(icon);
                }
                var texts = Ui.Column();
                texts.style.flexShrink = 1f;
                texts.Add(Ui.Text(term.Title, 15f, term.Color, true));
                var definition = Ui.Text(term.Definition, 12f, Theme.Text);
                definition.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(definition);
                int section = _sections.FindIndex(x => x.Title == term.Section);
                if (section >= 0)
                {
                    var link = Ui.Button($"Раздел «{term.Section}»", () => Select(section), ButtonKind.Ghost, 12f);
                    link.style.minHeight = 26f;
                    link.style.marginTop = 4f;
                    link.style.alignSelf = Align.FlexStart;
                    Ui.Padding(link, 2f, 8f);
                    texts.Add(link);
                }
                card.Add(texts);
                _termCards[term.Id] = card;
                grid.Add(card);
            }
            s.Add(grid);
            return s;
        }
    }
}
