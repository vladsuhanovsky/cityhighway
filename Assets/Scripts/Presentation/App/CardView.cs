using RailGame.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RailGame.Presentation.App
{
    /// <summary>Карта вагона: цвет, подпись, количество, радужная полоса у локомотива, «рубашка».</summary>
    public sealed class CardView : VisualElement
    {
        readonly Label _name;
        readonly Label _count;
        readonly VisualElement _stripes;
        readonly VisualElement _back;
        readonly VisualElement _dim;
        readonly float _countSize;

        public CardColor? Color { get; private set; }

        public CardView(float width, float height, bool withCount)
        {
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.overflow = Overflow.Hidden;
            style.flexDirection = FlexDirection.Column;
            Ui.Radius(this, 8f);
            Ui.Border(this, 2f, new UnityEngine.Color(0f, 0f, 0f, 0.35f));

            _name = Ui.Text("", 11f, null, true);
            _name.style.unityTextAlign = TextAnchor.UpperCenter;
            _name.style.whiteSpace = WhiteSpace.NoWrap;
            _name.style.marginTop = 6f;
            _name.style.overflow = Overflow.Hidden;
            Add(_name);

            _countSize = Mathf.Round(height * 0.3f);
            _count = Ui.Text("", _countSize, null, true);
            _count.style.unityTextAlign = TextAnchor.MiddleCenter;
            _count.style.flexGrow = 1f;
            Ui.Show(_count, withCount);
            Add(_count);

            _stripes = Ui.Row();
            _stripes.style.position = Position.Absolute;
            _stripes.style.left = 6f;
            _stripes.style.right = 6f;
            _stripes.style.bottom = 8f;
            _stripes.style.height = 7f;
            foreach (var c in new[] { CardColor.Red, CardColor.Orange, CardColor.Yellow, CardColor.Green, CardColor.Blue, CardColor.Purple })
            {
                var stripe = Ui.Box();
                stripe.style.flexGrow = 1f;
                stripe.style.height = 7f;
                stripe.style.backgroundColor = Palette.Card(c);
                _stripes.Add(stripe);
            }
            Add(_stripes);

            // Рубашка: тёмно-синяя с латунной рамкой — для карт из колоды, которых не видно.
            _back = Ui.Box();
            Ui.Fill(_back);
            _back.style.backgroundColor = new UnityEngine.Color(0.13f, 0.20f, 0.36f);
            var inner = Ui.Box();
            Ui.Fill(inner);
            Ui.Margin(inner, 6f);
            Ui.Radius(inner, 5f);
            Ui.Border(inner, 2f, new UnityEngine.Color(0.93f, 0.68f, 0.26f, 0.8f));
            _back.Add(inner);
            Ui.Show(_back, false);
            Add(_back);

            _dim = Ui.Box();
            Ui.Fill(_dim);
            _dim.style.backgroundColor = new UnityEngine.Color(0.08f, 0.09f, 0.11f, 0.62f);
            Ui.Show(_dim, false);
            Add(_dim);
        }

        /// <summary>color = null — пустая ячейка.</summary>
        public void Set(CardColor? color, int? count = null, bool dim = false)
        {
            Color = color;
            Ui.Show(_back, false);

            if (!color.HasValue)
            {
                style.backgroundColor = new UnityEngine.Color(1f, 1f, 1f, 0.03f);
                Ui.Border(this, 2f, new UnityEngine.Color(1f, 1f, 1f, 0.12f));
                _name.text = "";
                _count.text = "";
                Ui.Show(_stripes, false);
                Ui.Show(_dim, false);
                return;
            }

            var fill = Palette.Card(color.Value);
            var text = Palette.TextOn(fill);
            style.backgroundColor = fill;
            Ui.Border(this, 2f, new UnityEngine.Color(0f, 0f, 0f, 0.35f));
            _name.style.color = text;
            _count.style.color = text;
            _name.text = Palette.CardTitle(color.Value);
            _count.text = count.HasValue ? count.Value.ToString() : "";
            Ui.Show(_stripes, color.Value == CardColor.Locomotive);
            Ui.Show(_dim, dim);
        }

        public void ShowBack()
        {
            Color = null;
            Ui.Show(_back, true);
            Ui.Show(_dim, false);
            Ui.Show(_stripes, false);
            _name.text = "";
            _count.text = "";
        }

        /// <summary>Короткий «толчок» числа, когда карта прилетела в руку. k — 0..1.</summary>
        public void Pulse(float k)
        {
            float bump = Mathf.Sin(k * Mathf.PI);
            _count.style.fontSize = _countSize * (1f + 0.35f * bump);
            style.borderTopColor = UnityEngine.Color.Lerp(new UnityEngine.Color(0f, 0f, 0f, 0.35f), Theme.AccentHover, bump);
            style.borderBottomColor = style.borderTopColor;
            style.borderLeftColor = style.borderTopColor;
            style.borderRightColor = style.borderTopColor;
        }
    }
}
