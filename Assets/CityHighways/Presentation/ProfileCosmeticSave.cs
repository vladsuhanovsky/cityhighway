using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>Local-only transaction for the company, fleet and exhibition editors.
    /// Their write operations replace records instead of editing existing ones.
    /// The original record references therefore form a rollback snapshot.</summary>
    public static class ProfileCosmeticSave
    {
        public static bool TryCommit(ProfileBook book, PlayerProfile profile,
            Func<bool> change, Func<bool> persist)
        {
            if (book?.profiles == null || profile == null || !book.profiles.Contains(profile) ||
                string.IsNullOrEmpty(CompanyIdentity.OwnerKey(profile)) ||
                change == null || persist == null) return false;
            var previous = book.companies == null ? null : new List<CompanyIdentityRecord>(book.companies);
            bool committed = false;
            try
            {
                committed = change() && persist();
                return committed;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Local cosmetic save failed: " + ex.Message);
                return false;
            }
            finally
            {
                if (!committed) book.companies = previous;
            }
        }
    }
}
