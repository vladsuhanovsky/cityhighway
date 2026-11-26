using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityHighways.Presentation
{
    [Serializable]
    public sealed class FleetPaintRecord
    {
        public string vehicle, scheme;
        public bool hideEmblem;
    }

    /// <summary>Cosmetic catalog only. No vehicles are purchased or consumed in this garage.</summary>
    public sealed class FleetLivery
    {
        static readonly string[] Vehicles = { "paver", "mixer", "grader", "crane" };
        static readonly string[] Schemes = { "company", "reverse", "solid" };
        public static string[] VehicleIds => (string[])Vehicles.Clone();
        public static string[] SchemeIds => (string[])Schemes.Clone();

        public string Vehicle { get; }
        public string Scheme { get; }
        public bool HideEmblem { get; }
        public string Emblem { get; }
        public string CompanyName { get; }
        public Color Primary { get; }
        public Color Secondary { get; }

        FleetLivery(string vehicle, CompanyIdentity company, string scheme, bool hideEmblem)
        {
            Vehicle = vehicle;
            Scheme = Schemes.Contains(scheme) ? scheme : "company";
            HideEmblem = hideEmblem;
            CompanyName = company.Name;
            Emblem = hideEmblem ? "" : company.Emblem;
            Primary = CompanyIdentity.Paint(Scheme == "reverse" ? company.Secondary : company.Primary);
            Secondary = CompanyIdentity.Paint(Scheme == "solid" ? company.Primary :
                Scheme == "reverse" ? company.Primary : company.Secondary);
        }

        public static FleetLivery Preview(ProfileBook book, PlayerProfile profile, string vehicle, string scheme, bool hideEmblem)
        {
            if (!Vehicles.Contains(vehicle)) return null;
            return new FleetLivery(vehicle, CompanyIdentity.Read(book, profile), scheme, hideEmblem);
        }

        static CompanyIdentityRecord Record(ProfileBook book, PlayerProfile profile)
        {
            string key = CompanyIdentity.OwnerKey(profile);
            return string.IsNullOrEmpty(key) ? null : book?.companies?.FirstOrDefault(r => r != null && r.owner == key);
        }

        public static FleetLivery Read(ProfileBook book, PlayerProfile profile, string vehicle)
        {
            var raw = Record(book, profile)?.fleet?.FirstOrDefault(r => r != null && r.vehicle == vehicle);
            return Preview(book, profile, vehicle, raw?.scheme, raw?.hideEmblem ?? false);
        }

        // Writes a fresh bounded record and array, so callers can roll back a failed disk save
        // using a shallow copy of the previous company list. No mutation to PlayerProfile.
        public static bool Store(ProfileBook book, PlayerProfile profile, string vehicle, string scheme, bool hideEmblem)
        {
            if (book == null || string.IsNullOrEmpty(CompanyIdentity.OwnerKey(profile)) || !Vehicles.Contains(vehicle)) return false;
            var next = Preview(book, profile, vehicle, scheme, hideEmblem);
            var variants = new List<FleetPaintRecord>();
            foreach (string id in Vehicles)
            {
                var value = id == vehicle ? next : Read(book, profile, id);
                // Defaults need not occupy disk space.
                if (value.Scheme != "company" || value.HideEmblem)
                    variants.Add(new FleetPaintRecord { vehicle = id, scheme = value.Scheme, hideEmblem = value.HideEmblem });
            }
            CompanyIdentity.Store(book, profile, CompanyIdentity.Read(book, profile));
            Record(book, profile).fleet = variants.ToArray();
            return true;
        }
    }
}
