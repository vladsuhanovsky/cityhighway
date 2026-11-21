using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>Три режима игры — так делятся обучение, правила и счётчик партий в меню.</summary>
    public enum GameMode { Magnate, Classic, Coop }

    /// <summary>Переключатель режимов: три вкладки со значком, названием и одной строкой о режиме.</summary>
    public static class ModeTabs
    {
        public static string Title(GameMode mode) =>
            LocalizationService.Language == "en"
                ? (mode == GameMode.Magnate ? "Logistics Magnate" : mode == GameMode.Coop ? "Cooperative" : "Competitive")
                : (mode == GameMode.Magnate ? "Логистический магнат" : mode == GameMode.Coop ? "Кооператив" : "Соревнование");

        public static string Tagline(GameMode mode) =>
            LocalizationService.Language == "en"
                ? (mode == GameMode.Magnate ? "Trucks, cargo and hubs in a newly generated city" : mode == GameMode.Coop ? "All companies join forces against the city" : "Roads, City Hall contracts and tolls")
                : (mode == GameMode.Magnate ? "Фуры, грузы и хабы в новом городе каждый раз" : mode == GameMode.Coop ? "Все компании — одна команда против города" : "Дороги, контракты мэрии и плата за проезд");

        public static MenuIcon.Glyph Glyph(GameMode mode) =>
            mode == GameMode.Magnate ? MenuIcon.Glyph.Road : mode == GameMode.Coop ? MenuIcon.Glyph.Skyline : MenuIcon.Glyph.Trophy;

        /// <summary>Режим записи журнала партий: magnate, coop или classic (у старых записей — по отметке coop).</summary>
        public static GameMode Of(GameStat stat)
        {
            if (stat == null) return GameMode.Classic;
            if (stat.mode == "magnate") return GameMode.Magnate;
            if (stat.mode == "coop" || (string.IsNullOrEmpty(stat.mode) && stat.coop)) return GameMode.Coop;
            return GameMode.Classic;
        }

        public static string Key(GameMode mode) => mode == GameMode.Magnate ? "magnate" : mode == GameMode.Coop ? "coop" : "classic";

        /// <summary>Ряд из трёх вкладок; note — подпись под названием (например, «12 партий»), по умолчанию — строка о режиме.</summary>
        public static VisualElement Build(GameMode current, Action<GameMode> onSelect, Func<GameMode, string> note = null)
        {
            var row = Ui.Row();
            row.style.marginBottom = 14f;
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                bool active = mode == current;
                var tab = Ui.Row();
                tab.pickingMode = PickingMode.Position;
                tab.style.flexGrow = 1f;
                tab.style.flexBasis = 0f;
                tab.style.alignItems = Align.Center;
                tab.style.marginRight = mode == GameMode.Coop ? 0f : 10f;
                tab.style.backgroundColor = active ? new Color(Theme.Gold.r, Theme.Gold.g, Theme.Gold.b, 0.16f) : Theme.Panel;
                Ui.Radius(tab, 12f);
                Ui.Padding(tab, 10f, 14f);
                Ui.Border(tab, active ? 2f : 1f, active ? Theme.Gold : Theme.Line);
                Ui.Transitions(tab, "background-color", "translate");
                var icon = new MenuIcon(Glyph(mode), 26f, active ? Theme.Gold : Theme.TextDim);
                icon.style.marginRight = 12f;
                icon.pickingMode = PickingMode.Ignore;
                tab.Add(icon);
                var texts = Ui.Column();
                texts.pickingMode = PickingMode.Ignore;
                texts.style.flexShrink = 1f;
                texts.Add(Ui.Text(Title(mode), 16f, active ? Theme.Gold : Theme.Text, true));
                var line = Ui.Text(note?.Invoke(mode) ?? Tagline(mode), 12f, Theme.TextDim);
                line.style.whiteSpace = WhiteSpace.Normal;
                texts.Add(line);
                tab.Add(texts);
                var chosen = mode;
                tab.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (!active) tab.style.backgroundColor = Theme.PanelHover;
                    tab.style.translate = new Translate(new Length(0f), new Length(-2f));
                    Ui.Hovered?.Invoke();
                });
                tab.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (!active) tab.style.backgroundColor = Theme.Panel;
                    tab.style.translate = new Translate(new Length(0f), new Length(0f));
                });
                tab.RegisterCallback<ClickEvent>(_ =>
                {
                    if (active) return;
                    Ui.ButtonClicked?.Invoke();
                    onSelect(chosen);
                });
                row.Add(tab);
            }
            return row;
        }
    }
}
