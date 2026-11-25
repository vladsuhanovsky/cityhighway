using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace CityHighways.Net
{
    /// <summary>In-memory reconnect keys scoped to local profile object, platform account and exact destination.</summary>
    public sealed class ReconnectCredentialCache
    {
        readonly ConditionalWeakTable<object, Dictionary<string, string>> _profiles =
            new ConditionalWeakTable<object, Dictionary<string, string>>();

        public static string NewKey()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        public static bool IsValid(string key)
        {
            if (key == null || key.Length != 64) return false;
            foreach (char c in key)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        public string For(object profile, string account, string destination)
        {
            if (profile == null || string.IsNullOrWhiteSpace(destination)) return NewKey();
            var keys = _profiles.GetValue(profile, _ => new Dictionary<string, string>(StringComparer.Ordinal));
            account = account ?? "";
            string scope = account.Length + ":" + account + destination;
            if (!keys.TryGetValue(scope, out string key))
            {
                key = NewKey();
                keys.Add(scope, key);
            }
            return key;
        }
    }
}
