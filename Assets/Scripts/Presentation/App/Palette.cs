using RailGame.Core;
using UnityEngine;

namespace RailGame.Presentation.App
{
    /// <summary>Цвета карт вагонов, маршрутов и игроков.</summary>
    public static class Palette
    {
        public static readonly Color GrayRoute = new Color(0.66f, 0.66f, 0.66f);

        /// <summary>Цвета игроков — не совпадают с цветами карт, чтобы занятые маршруты было видно.</summary>
        public static readonly Color[] Players =
        {
            new Color(0.10f, 0.78f, 0.80f), // бирюзовый
            new Color(0.90f, 0.25f, 0.70f), // малиновый
            new Color(0.55f, 0.36f, 0.20f), // коричневый
            new Color(0.62f, 0.88f, 0.12f), // салатовый
            new Color(0.42f, 0.47f, 0.60f)  // графитовый
        };

        public static Color PlayerColor(int index) => Players[((index % Players.Length) + Players.Length) % Players.Length];

        public static Color Card(CardColor color)
        {
            switch (color)
            {
                case CardColor.Red: return new Color(0.86f, 0.22f, 0.20f);
                case CardColor.Orange: return new Color(0.96f, 0.56f, 0.16f);
                case CardColor.Yellow: return new Color(0.96f, 0.86f, 0.22f);
                case CardColor.Green: return new Color(0.30f, 0.68f, 0.32f);
                case CardColor.Blue: return new Color(0.24f, 0.45f, 0.86f);
                case CardColor.Purple: return new Color(0.58f, 0.34f, 0.76f);
                case CardColor.Black: return new Color(0.14f, 0.14f, 0.16f);
                case CardColor.White: return new Color(0.96f, 0.96f, 0.96f);
                default: return new Color(0.80f, 0.72f, 0.95f); // локомотив
            }
        }

        public static Color RouteColor(Route route) => route.Color.HasValue ? Card(route.Color.Value) : GrayRoute;

        /// <summary>Чёрный или белый текст — что лучше читается на этом фоне.</summary>
        public static Color TextOn(Color background)
        {
            float luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            return luminance > 0.6f ? new Color(0.1f, 0.1f, 0.1f) : Color.white;
        }

        public static string CardTitle(CardColor color)
        {
            string name = CardColors.RussianName(color);
            return char.ToUpper(name[0]) + name.Substring(1);
        }
    }
}
