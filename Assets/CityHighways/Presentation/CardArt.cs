using System.Collections.Generic;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Иллюстрации карт ресурсов из Resources/CityHighways/Cards (machinery, asphalt, gravel, concrete, steel).
    /// Название ресурса уже нарисовано на картинке. Если картинки нет — CardView рисует карту по-старому, цветом.
    /// </summary>
    public static class CardArt
    {
        static readonly Dictionary<Resource, Texture2D> Cache = new Dictionary<Resource, Texture2D>();

        public static Texture2D Get(Resource resource)
        {
            if (Cache.TryGetValue(resource, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>("CityHighways/Cards/" + FileName(resource));
            Cache[resource] = texture;
            return texture;
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
