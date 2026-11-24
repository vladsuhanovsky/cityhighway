using System;
using System.IO;
using CityHighways.Net;
using UnityEngine;

namespace CityHighways.Presentation
{
    public static partial class SocialCloud
    {
        static SocialReportOutbox _reportOutbox;
        static bool _reportInFlight;

        static SocialReportOutbox ReportOutbox
        {
            get
            {
                if (_reportOutbox == null)
                {
                    string path = Path.Combine(Application.persistentDataPath,
                        "social", "pending-reports.json");
                    _reportOutbox = new SocialReportOutbox(path);
                }
                return _reportOutbox;
            }
        }

        /// <summary>Local pending reports, including ones from a different Steam account.</summary>
        public static int PendingReports => ReportOutbox.Count;

        /// <summary>Count only the signed-in account's reports in UI.</summary>
        public static int PendingReportsForCurrentAccount
        {
            get
            {
                string owner = NetPlatforms.Steam?.SteamId;
                if (!SocialReportOutbox.ValidOwner(owner)) return 0;
                int count = 0;
                foreach (var entry in ReportOutbox.Entries)
                    if (entry.ownerSteamId == owner) count++;
                return count;
            }
        }

        static void QueueReport(Request request, string key)
        {
            // A report can be persisted while the profile is still syncing,
            // but it MUST be associated with an established Steam identity.
            // Never store a Web API ticket on disk.
            var steam = NetPlatforms.Steam;
            string owner = steam != null ? steam.SteamId : null;
            if (!SocialReportOutbox.ValidOwner(owner)) return;

            request.ticket = null;
            request.claimedId = null;
            request.version = Application.version;
            bool queued = ReportOutbox.Enqueue(owner, key,
                JsonUtility.ToJson(request), DateTime.UtcNow.Ticks);
            if (!queued)
            {
                News.Add(LocalizationService.Language == "en"
                    ? "Could not store the game report for later upload (queue is full or disk is unavailable)."
                    : "Не удалось сохранить отчёт партии для отправки (очередь заполнена или диск недоступен).");
                Changed?.Invoke();
                return;
            }
            TrySendQueuedReport();
        }

        /// <summary>Called from the existing SocialCloud.Tick loop and after successful uploads.</summary>
        static void TrySendQueuedReport()
        {
            if (_reportInFlight || !Available) return;
            var steam = NetPlatforms.Steam;
            if (steam == null || !SocialReportOutbox.ValidOwner(steam.SteamId)) return;
            var entry = ReportOutbox.Next(steam.SteamId, DateTime.UtcNow.Ticks);
            if (entry == null) return;

            Request request;
            try
            {
                request = JsonUtility.FromJson<Request>(entry.requestJson);
                if (request == null || request.action != "report")
                    throw new InvalidOperationException("Invalid pending social report");
                request.ticket = null;
                request.claimedId = null;
                request.skipClub = !SocialReportOutbox.SameUtcWeek(
                    entry.createdUtcTicks, DateTime.UtcNow.Ticks);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Discarding unreadable social report: " + error.Message);
                ReportOutbox.Complete(entry);
                return;
            }

            _reportInFlight = true;
            Send<ReportResponse>(request, response =>
            {
                _reportInFlight = false;
                // Acknowledge only a successful server response; never remove
                // a report merely because a UnityWebRequest was attempted.
                ReportOutbox.Complete(entry);
                AfterReport(response);
                TrySendQueuedReport();
            }, reportFailure: (message, status) =>
            {
                _reportInFlight = false;
                // The report remains on disk until acknowledged, even for
                // temporary authentication, schema or server rollout errors.
                ReportOutbox.Reschedule(entry, DateTime.UtcNow.Ticks, status);
                Debug.LogWarning("Social report will retry: " + message);
            }, expectedSteamId: entry.ownerSteamId);
        }
    }
}
