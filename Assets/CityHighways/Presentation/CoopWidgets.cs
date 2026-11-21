using System;
using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Панель кооператива «Город против всех» в правом верхнем углу поля: сколько заказов выполнено из нужного,
    /// шкала кризиса (делениями, зелёный → оранжевый → красный) и открытые заказы города с таймером в кругах.
    /// Наведение на заказ подсвечивает его районы на карте (hover(contract), уход — hover(null)).
    /// Заголовок сворачивает панель, чтобы не мешала смотреть на карту.
    /// </summary>
    public sealed class CoopPanel : VisualElement
    {
        readonly Action<Contract> _hover;
        readonly Label _score;
        readonly Label _titleLabel;
        readonly Label _crisisCaption;
        readonly VisualElement _meter;
        readonly Label _crisisLabel;
        readonly VisualElement _orders;
        readonly VisualElement _body;
        readonly MenuIcon _chevron;
        readonly List<VisualElement> _cells = new List<VisualElement>();
        bool _collapsed;
        string _signature;

        public CoopPanel(Action<Contract> hover)
        {
            _hover = hover;
            style.position = Position.Absolute;
            style.right = 14f;
            style.top = 14f;
            style.width = 300f;
            style.backgroundColor = Theme.PanelGlass;
            Ui.Radius(this, 12f);
            Ui.Border(this, 1f, Theme.Line);
            Ui.Padding(this, 10f, 12f);
            Ui.Shadow(this, 10f);

            var head = Ui.Row();
            head.pickingMode = PickingMode.Position;
            head.style.alignItems = Align.Center;
            var icon = new MenuIcon(MenuIcon.Glyph.Skyline, 20f, Theme.Gold);
            icon.style.marginRight = 8f;
            head.Add(icon);
            var title = Ui.Text(LocalizationService.Language == "en" ? "CITY VS EVERYONE" : "ГОРОД ПРОТИВ ВСЕХ", 12f, Theme.Gold, true);
            _titleLabel = title;
            title.style.letterSpacing = 1.2f;
            title.style.flexGrow = 1f;
            head.Add(title);
            _score = Ui.Number("", 15f, Theme.Text, true);
            _score.style.marginRight = 6f;
            head.Add(_score);
            _chevron = new MenuIcon(MenuIcon.Glyph.Chevron, 14f, Theme.TextDim) { Level = 1 };
            head.Add(_chevron);
            head.RegisterCallback<ClickEvent>(_ =>
            {
                _collapsed = !_collapsed;
                _chevron.Level = _collapsed ? 0 : 1;
                Ui.Show(_body, !_collapsed);
                Ui.ButtonClicked?.Invoke();
            });
            Add(head);

            _body = Ui.Column();
            Add(_body);
            var crisisRow = Ui.Row();
            crisisRow.style.alignItems = Align.Center;
            crisisRow.style.marginTop = 8f;
            var crisisCaption = Ui.Text(LocalizationService.Language == "en" ? "Crisis" : "Кризис", 12f, Theme.TextDim, true);
            _crisisCaption = crisisCaption;
            crisisCaption.style.width = 52f;
            crisisRow.Add(crisisCaption);
            _meter = Ui.Row();
            _meter.style.flexGrow = 1f;
            _meter.style.height = 10f;
            crisisRow.Add(_meter);
            _crisisLabel = Ui.Number("", 12f, Theme.Text, true);
            _crisisLabel.style.marginLeft = 8f;
            _crisisLabel.style.width = 38f;
            _crisisLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            crisisRow.Add(_crisisLabel);
            _body.Add(crisisRow);

            _orders = Ui.Column();
            _orders.style.marginTop = 6f;
            _body.Add(_orders);
        }

        public static Color CrisisColor(float t) =>
            t < 0.5f ? Color.Lerp(GameColors.Success, GameColors.Warning, t * 2f) : Color.Lerp(GameColors.Warning, GameColors.Danger, (t - 0.5f) * 2f);

        public void Set(Game game)
        {
            if (game == null || !game.IsCooperative)
            {
                Ui.Show(this, false);
                return;
            }
            Ui.Show(this, true);
            bool english = LocalizationService.Language == "en";
            _titleLabel.text = english ? "CITY VS EVERYONE" : "ГОРОД ПРОТИВ ВСЕХ";
            _crisisCaption.text = english ? "Crisis" : "Кризис";
            _score.text = $"{game.OrdersDone}/{game.OrdersTarget}";

            // Шкала: столько делений, сколько максимум кризиса.
            if (_cells.Count != game.CrisisMax)
            {
                _meter.Clear();
                _cells.Clear();
                for (int i = 0; i < game.CrisisMax; i++)
                {
                    var cell = Ui.Box();
                    cell.style.flexGrow = 1f;
                    cell.style.marginRight = i < game.CrisisMax - 1 ? 2f : 0f;
                    Ui.Radius(cell, 2f);
                    Ui.Transitions(cell, 0.3f, "background-color");
                    _cells.Add(cell);
                    _meter.Add(cell);
                }
            }
            for (int i = 0; i < _cells.Count; i++)
                _cells[i].style.backgroundColor = i < game.Crisis
                    ? CrisisColor(i / (float)Mathf.Max(1, game.CrisisMax - 1))
                    : new Color(1f, 1f, 1f, 0.10f);
            _crisisLabel.text = $"{game.Crisis}/{game.CrisisMax}";
            _crisisLabel.style.color = CrisisColor(game.Crisis / (float)Mathf.Max(1, game.CrisisMax));

            // Карточки заказов пересобираем, только когда что-то изменилось.
            var signature = LocalizationService.Language + ";" + string.Join(";", Signature(game));
            if (signature == _signature) return;
            _signature = signature;
            _orders.Clear();
            if (game.CityOrders.Count == 0)
            {
                var none = Ui.Text(english ? "No city orders: the contract deck is empty." : "Заказов нет — колода контрактов пуста.", 12f, Theme.TextDim);
                none.style.whiteSpace = WhiteSpace.Normal;
                _orders.Add(none);
            }
            foreach (var order in game.CityOrders) _orders.Add(OrderRow(game, order));
        }

        static IEnumerable<string> Signature(Game game)
        {
            foreach (var order in game.CityOrders) yield return order.Contract.Id + ":" + order.RoundsLeft;
        }

        VisualElement OrderRow(Game game, CityOrder order)
        {
            var contract = order.Contract;
            var map = game.Map;
            bool urgent = order.RoundsLeft <= 1;
            var row = Ui.Row();
            row.pickingMode = PickingMode.Position;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 5f;
            row.style.backgroundColor = new Color(0f, 0f, 0f, 0.22f);
            Ui.Radius(row, 8f);
            Ui.Padding(row, 6f, 8f);
            row.style.borderLeftWidth = 4f;
            row.style.borderLeftColor = urgent ? GameColors.Danger : order.RoundsLeft <= 2 ? GameColors.Warning : GameColors.Success;
            Ui.Transitions(row, "background-color");

            var texts = Ui.Column();
            texts.style.flexGrow = 1f;
            texts.style.flexShrink = 1f;
            string route = $"{MagnateLocationText.Name(map.DistrictName(contract.DistrictA))} — {MagnateLocationText.Name(map.DistrictName(contract.DistrictB))}";
            var name = Ui.Text(route, 13f, Theme.Text, true);
            name.style.whiteSpace = WhiteSpace.Normal;
            texts.Add(name);
            if (contract.IsComplex)
                texts.Add(Ui.Text((LocalizationService.Language == "en" ? (contract.Kind == ContractKind.Transit ? "via " : "and ") : (contract.Kind == ContractKind.Transit ? "через " : "и ")) + MagnateLocationText.Name(map.DistrictName(contract.DistrictC)), 11f, Theme.TextDim));
            row.Add(texts);

            var right = Ui.Column();
            right.style.alignItems = Align.FlexEnd;
            right.style.marginLeft = 6f;
            right.Add(Ui.Number($"+{contract.Points}", 13f, Theme.Gold, true));
            var timer = Ui.Row();
            timer.style.alignItems = Align.Center;
            var clock = new MenuIcon(MenuIcon.Glyph.Clock, 12f, urgent ? GameColors.Danger : Theme.TextDim);
            clock.style.marginRight = 3f;
            timer.Add(clock);
            timer.Add(Ui.Text(LocalizationService.Language == "en" ? (order.RoundsLeft == 1 ? "final round" : $"{order.RoundsLeft} rounds") : (order.RoundsLeft == 1 ? "последний круг" : $"{order.RoundsLeft} {Plural(order.RoundsLeft)}"), 11f,
                urgent ? GameColors.Danger : Theme.TextDim, urgent));
            right.Add(timer);
            row.Add(right);

            row.RegisterCallback<PointerEnterEvent>(_ =>
            {
                row.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
                _hover?.Invoke(contract);
            });
            row.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                row.style.backgroundColor = new Color(0f, 0f, 0f, 0.22f);
                _hover?.Invoke(null);
            });
            return row;
        }

        static string Plural(int n)
        {
            int last = n % 10, lastTwo = n % 100;
            if (last == 1 && lastTwo != 11) return "круг";
            if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) return "круга";
            return "кругов";
        }
    }
}
