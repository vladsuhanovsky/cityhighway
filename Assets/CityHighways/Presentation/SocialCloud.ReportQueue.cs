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
        // The server accepted this report but the local removal failed.
        // Retry the disk operation, never the already-acknowledged HTTP POST.
        static SocialReportOutbox.Entry _acknowledgedPending;
        static long _diskRetryAfterUtcTicks;
        const long DiskRetryTicks = 30 * TimeSpan.TicksPerSecond;

        static void DeferAfterDiskError()
        {
            _diskRetryAfterUtcTicks = DateTime.UtcNow.Ticks + DiskRetryTicks;
            Debug.LogWarning("Social report queue disk update failed; retrying in 30 seconds.");
        }

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
            Changed?.Invoke();
            TrySendQueuedReport();
        }

        /// <summary>Called from the existing SocialCloud.Tick loop and after successful uploads.</summary>
        static void TrySendQueuedReport()
        {
            if (_reportInFlight) return;
            long nowTicks = DateTime.UtcNow.Ticks;
            if (nowTicks < _diskRetryAfterUtcTicks) return;

            if (_acknowledgedPending != null)
            {
                if (!ReportOutbox.Complete(_acknowledgedPending))
                {
                    DeferAfterDiskError();
                    return;
                }
                _acknowledgedPending = null;
                Changed?.Invoke();
            }

            if (!Available) return;
            var steam = NetPlatforms.Steam;
            if (steam == null || !SocialReportOutbox.ValidOwner(steam.SteamId)) return;
            var entry = ReportOutbox.Next(steam.SteamId, nowTicks);
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
                if (!ReportOutbox.Complete(entry))
                {
                    _acknowledgedPending = entry;
                    DeferAfterDiskError();
                }
                Changed?.Invoke();
                return;
            }

            _reportInFlight = true;
            Send<ReportResponse>(request, response =>
            {
                _reportInFlight = false;
                if (response == null)
                {
                    if (!ReportOutbox.Reschedule(entry, DateTime.UtcNow.Ticks, 0))
                        DeferAfterDiskError();
                    Debug.LogWarning("Social report response was empty; keeping it for retry.");
                    return;
                }

                // Acknowledge only a successful server response. If removing
                // that ACK from disk fails, retry the write without resending
                // the same HTTP report every frame.
                if (!ReportOutbox.Complete(entry))
                {
                    _acknowledgedPending = entry;
                    DeferAfterDiskError();
                }
                Changed?.Invoke();
                AfterReport(response);
                TrySendQueuedReport();
            }, reportFailure: (message, status) =>
            {
                _reportInFlight = false;
                // The report remains on disk until acknowledged, even for
                // temporary authentication, schema or server rollout errors.
                if (!ReportOutbox.Reschedule(entry, DateTime.UtcNow.Ticks, status))
                    DeferAfterDiskError();
                Debug.LogWarning("Social report will retry: " + message);
            }, expectedSteamId: entry.ownerSteamId);
        }
    }
}
