using System;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>Local match cosmetic mapping. Unknown or ambiguous seat/profile identity fails closed.</summary>
    public static class ConstructionFleetPolicy
    {
        public static string VehicleFor(Resource? resource)
        {
            switch (resource)
            {
                case Resource.Concrete: return "mixer";
                case Resource.Gravel: return "grader";
                case Resource.Steel: return "crane";
                default: return "paver";
            }
        }

        public static PlayerProfile ForLocalSeat(ProfileBook book, GameSetup setup, int owner, string playerName, bool online)
        {
            if (online || setup == null || setup.practice || setup.tutorial || setup.IsCampaign ||
                setup.players == null || owner < 0 || owner >= setup.players.Count ||
                string.IsNullOrWhiteSpace(playerName) || book?.profiles == null) return null;
            var slot = setup.players[owner];
            if (slot == null || slot.isBot || !SameName(slot.name, playerName)) return null;
            int matchingSeats = 0;
            foreach (var other in setup.players)
                if (other != null && SameName(other.name, playerName)) matchingSeats++;
            if (matchingSeats != 1) return null;
            PlayerProfile profile = null;
            foreach (var candidate in book.profiles)
            {
                if (candidate == null || !SameName(candidate.name, playerName)) continue;
                if (profile != null) return null;
                profile = candidate;
            }
            // No implicit default fleet for a seat without a local profile.
            return profile;
        }

        public static FleetLivery Resolve(ProfileBook book, GameSetup setup, int owner, string playerName, bool online, Resource? resource)
        {
            var profile = ForLocalSeat(book, setup, owner, playerName, online);
            return profile == null ? null : FleetLivery.ForActionOwner(book, profile, VehicleFor(resource));
        }

        public static ProfileAppearance ResolveWalker(ProfileBook book, GameSetup setup, int owner,
            string playerName, bool online, bool isBot)
        {
            if (isBot || setup == null || setup.forceMagnateTutorial || setup.magnateTutorialMap) return null;
            var profile = ForLocalSeat(book, setup, owner, playerName, online);
            return profile == null ? null : ProfileAppearance.FromProfile(profile);
        }

        public static FleetLivery ResolveMagnate(ProfileBook book, GameSetup setup, int owner,
            string playerName, int publicOwner, bool isBot, Resource? resource)
        {
            if (isBot || owner == publicOwner || setup == null || setup.forceMagnateTutorial ||
                setup.magnateTutorialMap) return null;
            return Resolve(book, setup, owner, playerName, false, resource);
        }

        static bool SameName(string a, string b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
