using System;

namespace CityHighways.Core
{
    /// <summary>
    /// A fresh random ID for one Logistics Magnate game instance.
    /// Retries of the same finished game reuse this ID; a new game gets a new
    /// one. This prevents accidental duplicate credit, not malicious clients
    /// inventing IDs or scores.
    /// </summary>
    public static class MagnateRunIdentity
    {
        public static string Create() => Guid.NewGuid().ToString("N");

        public static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (!((ch >= '0' && ch <= '9') ||
                      (ch >= 'a' && ch <= 'f')))
                    return false;
            }
            return Guid.TryParseExact(value, "N", out var parsed) &&
                   parsed != Guid.Empty;
        }
    }
}
