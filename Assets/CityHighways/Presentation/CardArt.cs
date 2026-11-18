using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Иллюстрации карт ресурсов. Сначала ищется новая картинка без надписи из Scenario —
    /// Resources/CityHighways/Cards/Art/&lt;имя&gt; (название ресурса тогда пишет сама игра, NeedsTitle = true),
    /// затем старая, где название уже нарисовано: Resources/CityHighways/Cards/&lt;имя&gt;.
    /// Если картинок нет — CardView рисует карту по-старому, цветом.
    /// </summary>
    public static class CardArt
    {
        static readonly Dictionary<Resource, Texture2D> Cache = new Dictionary<Resource, Texture2D>();
        static readonly HashSet<Resource> Textless = new HashSet<Resource>();

        public static Texture2D Get(Resource resource)
        {
            if (Cache.TryGetValue(resource, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>(GeneratedArt.CardArtFolder + FileName(resource));
            if (texture != null) Textless.Add(resource);
            else texture = Resources.Load<Texture2D>("CityHighways/Cards/" + FileName(resource));
            Cache[resource] = texture;
            return texture;
        }

        /// <summary>Картинка без надписи — название ресурса рисует игра поверх.</summary>
        public static bool NeedsTitle(Resource resource)
        {
            Get(resource);
            return Textless.Contains(resource);
        }

        static string FileName(Resource resource)
        {
            switch (resource)
            {
                case Resource.Asphalt: return "asphalt";
                case Resource.Concrete: return "concrete";
                case Resource.Steel: return "steel";
                case Resource.Gravel: return "gravel";
                default: return "machinery";
            }
        }
    }
}
