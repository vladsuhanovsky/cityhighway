using CityHighways.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Карта ресурса: цвет, подпись, количество, узор внизу (разметка, полосы техники), «рубашка».</summary>
    public sealed class CardView : VisualElement
    {
        readonly Label _name;
        readonly Label _count;
        readonly Label _badge;   // количество поверх иллюстрации — в углу, чтобы не закрывать картинку
        readonly VisualElement _hazard;
        readonly VisualElement _marking;
        readonly VisualElement _back;
        readonly VisualElement _dim;
        readonly float _countSize;

        public Resource? Resource { get; private set; }

        public CardView(float width, float height, bool withCount)
        {
            style.width = width;
            style.height = height;
            style.flexShrink = 0f;
            style.overflow = Overflow.Hidden;
            style.flexDirection = FlexDirection.Column;
            Ui.Radius(this, 8f);
            Ui.Border(this, 2f, new Color(0f, 0f, 0f, 0.35f));

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

            _badge = Ui.Text("", Mathf.Max(12f, Mathf.Round(height * 0.18f)), Color.white, true);
            _badge.style.position = Position.Absolute;
            _badge.style.right = 4f;
            _badge.style.bottom = 4f;
            _badge.style.minWidth = height * 0.24f;
            _badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            _badge.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            _badge.style.paddingLeft = 4f;
            _badge.style.paddingRight = 4f;
            Ui.Radius(_badge, 6f);
            Ui.Border(_badge, 1f, new Color(1f, 1f, 1f, 0.25f));
            Ui.Show(_badge, false);
            Add(_badge);

            // Чёрно-жёлтые полосы, как на дорожной технике.
            _hazard = Ui.Row();
            _hazard.style.position = Position.Absolute;
            _hazard.style.left = 6f;
            _hazard.style.right = 6f;
            _hazard.style.bottom = 8f;
            _hazard.style.height = 8f;
            for (int i = 0; i < 6; i++)
            {
                var stripe = Ui.Box();
                stripe.style.flexGrow = 1f;
                stripe.style.height = 8f;
                stripe.style.backgroundColor = i % 2 == 0 ? new Color(0.12f, 0.12f, 0.12f) : new Color(0.99f, 0.84f, 0.20f);
                _hazard.Add(stripe);
            }
            Add(_hazard);

            // Белая прерывистая разметка — для асфальта.
            _marking = Ui.Row();
            _marking.style.position = Position.Absolute;
            _marking.style.left = 8f;
            _marking.style.right = 8f;
            _marking.style.bottom = 11f;
            _marking.style.height = 3f;
            _marking.style.justifyContent = Justify.SpaceBetween;
            for (int i = 0; i < 4; i++)
            {
                var dash = Ui.Box();
                dash.style.width = Length.Percent(18f);
                dash.style.height = 3f;
                dash.style.backgroundColor = new Color(1f, 1f, 1f, 0.85f);
                _marking.Add(dash);
            }
            Add(_marking);

            _back = Ui.Box();
            Ui.Fill(_back);
            _back.style.backgroundColor = new Color(0.16f, 0.22f, 0.30f);
            var inner = Ui.Box();
            Ui.Fill(inner);
            Ui.Margin(inner, 6f);
            Ui.Radius(inner, 5f);
            Ui.Border(inner, 2f, new Color(0.99f, 0.74f, 0.10f, 0.8f));
            _back.Add(inner);
            Ui.Show(_back, false);
            Add(_back);

            _dim = Ui.Box();
            Ui.Fill(_dim);
            _dim.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.62f);
            Ui.Show(_dim, false);
            Add(_dim);
        }

        /// <summary>resource = null — пустая ячейка.</summary>
        public void Set(Resource? resource, int? count = null, bool dim = false)
        {
            Resource = resource;
            Ui.Show(_back, false);

            if (!resource.HasValue)
            {
                style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
                Ui.Border(this, 2f, new Color(1f, 1f, 1f, 0.12f));
                _name.text = "";
                _count.text = "";
                style.backgroundImage = StyleKeyword.None;
                Ui.Show(_badge, false);
                Ui.Show(_hazard, false);
                Ui.Show(_marking, false);
                Ui.Show(_dim, false);
                return;
            }

            // Иллюстрация карты (если есть): название уже на картинке, количество — значком в углу.
            var art = CardArt.Get(resource.Value);
            if (art != null)
            {
                style.backgroundImage = art;
                style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                style.backgroundColor = new Color(0.12f, 0.13f, 0.17f);
                Ui.Border(this, 2f, new Color(0f, 0f, 0f, 0.45f));
                _name.text = "";
                _count.text = "";
                _badge.text = count.HasValue ? count.Value.ToString() : "";
                Ui.Show(_badge, count.HasValue);
                Ui.Show(_hazard, false);
                Ui.Show(_marking, false);
                Ui.Show(_dim, dim);
                return;
            }
            style.backgroundImage = StyleKeyword.None;
            Ui.Show(_badge, false);

            var fill = Palette.Card(resource.Value);
            var text = Palette.TextOn(fill);
            style.backgroundColor = fill;
            Ui.Border(this, 2f, new Color(0f, 0f, 0f, 0.35f));
            _name.style.color = text;
            _count.style.color = text;
            _name.text = Palette.CardTitle(resource.Value);
            _count.text = count.HasValue ? count.Value.ToString() : "";
            Ui.Show(_hazard, resource.Value == Core.Resource.Machinery);
            Ui.Show(_marking, resource.Value == Core.Resource.Asphalt);
            Ui.Show(_dim, dim);
        }

        public void ShowBack()
        {
            Resource = null;
            Ui.Show(_back, true);
            Ui.Show(_dim, false);
            Ui.Show(_hazard, false);
            Ui.Show(_marking, false);
            Ui.Show(_badge, false);
            style.backgroundImage = StyleKeyword.None;
            _name.text = "";
            _count.text = "";
        }

        public void Pulse(float k)
        {
            float bump = Mathf.Sin(k * Mathf.PI);
            _count.style.fontSize = _countSize * (1f + 0.35f * bump);
            _badge.style.scale = new Scale(Vector3.one * (1f + 0.3f * bump));
            var border = Color.Lerp(new Color(0f, 0f, 0f, 0.35f), Theme.AccentHover, bump);
            style.borderTopColor = border;
            style.borderBottomColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
        }
    }
}
