using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Аватар профиля: картинка (загруженная или из Steam) в скруглённом квадрате; без картинки — первая буква
    /// имени на цветном фоне.
    /// </summary>
    public sealed class AvatarView : VisualElement
    {
        readonly Label _letter;

        public AvatarView(float size)
        {
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0f;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.overflow = Overflow.Hidden;
            Ui.Radius(this, size * 0.22f);
            Ui.Border(this, Mathf.Max(2f, size * 0.04f), Theme.Accent);
            _letter = Ui.Text("", size * 0.45f, Theme.AccentText, true);
            _letter.pickingMode = PickingMode.Ignore;
            Add(_letter);
        }

        public void Set(PlayerProfile profile)
        {
            var picture = Profiles.AvatarOf(profile);
            if (picture != null)
            {
                style.backgroundImage = new StyleBackground(picture);
                style.backgroundColor = Theme.Panel;
                _letter.text = "";
            }
            else
            {
                style.backgroundImage = StyleKeyword.None;
                style.backgroundColor = Theme.Accent;
                string name = profile?.name ?? "";
                _letter.text = name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?";
            }
        }
    }
}
