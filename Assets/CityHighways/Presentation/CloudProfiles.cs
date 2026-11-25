using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Профили в облаке (Supabase): профиль игрока Steam хранится на сервере под ID, совпадающим с его SteamID
    /// (запись «steam:&lt;SteamID64&gt;»), чтобы у разработчика был доступ ко всем профилям через админ-панель
    /// (Server/admin/index.html).
    ///
    /// Как это работает: игра просит у Steam билет входа (GetAuthTicketForWebApi) и отправляет его вместе с профилем
    /// в функцию player-sync. Сервер проверяет билет у Steam — только так он узнаёт SteamID, подставить чужой нельзя.
    /// В ответ приходят бан и действия администратора (начислить влияние, выдать или забрать вещь, сообщение) —
    /// игра применяет их и в следующий раз сообщает, какие применила.
    ///
    /// Синхронизация: при запуске, через 8 с после любого сохранения профиля и раз в 5 минут. Без Steam (запуск не
    /// из магазина) — ничего не отправляется. Ключ в коде — публичный (publishable): он не даёт доступа к таблицам,
    /// всё идёт через функцию.
    /// </summary>
    public static class CloudProfiles
    {
        public const string Url = "https://nswqxnjyxlrtfzdlounk.supabase.co";
        public const string PublishableKey = "sb_publishable_uawmnbX-MRpqMCeWBFZdCA_0yXpJ_qK";
        const string Function = "/functions/v1/player-sync";
        const string TicketIdentity = "cityhighways";

        [Serializable]
        class SyncRequest
        {
            public string platform;
            public string ticket;
            public string claimedId;
            public string version;
            public long[] applied;
            public PlayerProfile profile;
        }

        [Serializable]
        public class AdminAction
        {
            public long id;
            public string kind;
            public int amount;
            public string item;
            public string note;
        }

        [Serializable]
        class SyncResponse
        {
            public string id;
            public string platformUserId;
            public bool verified;
            public bool banned;
            public string banReason;
            public AdminAction[] actions;
            public string error;
        }

        static bool _started;
        static readonly CloudProfileSyncSchedule Schedule = new CloudProfileSyncSchedule();
        static string _observedSteamId;
        static int _accountEpoch;
        static float Now => Time.realtimeSinceStartup;

        static string CurrentSteamId()
        {
            var steam = NetPlatforms.Steam;
            return steam != null && steam.Available && SocialReportOutbox.ValidOwner(steam.SteamId)
                ? steam.SteamId : null;
        }

        static void EnsureSteamAccount()
        {
            string owner = CurrentSteamId();
            if (string.Equals(_observedSteamId, owner, StringComparison.Ordinal)) return;

            _observedSteamId = owner;
            unchecked { _accountEpoch++; }
            // A stale in-flight Steam request must not keep a new account
            // busy or apply another person's admin actions and sync receipt.
            Schedule.Reset(Now);
            ServerId = "";
            Problem = "";
            Messages.Clear();
            Changed?.Invoke();
        }

        /// <summary>ID записи на сервере («steam:…»), когда сервер его подтвердил; пусто — ещё не синхронизировано.</summary>
        public static string ServerId { get; private set; } = "";
        /// <summary>Последняя ошибка синхронизации (для подсказки); пусто — всё в порядке.</summary>
        public static string Problem { get; private set; } = "";
        /// <summary>Сообщения администратора, ещё не показанные игроку.</summary>
        public static readonly List<string> Messages = new List<string>();
        /// <summary>Профиль изменился с сервера (выдали влияние, вещь, бан) — меню стоит перерисовать.</summary>
        public static event Action Changed;

        static PlayerProfile Target
        {
            get
            {
                var steam = NetPlatforms.Steam;
                if (steam == null || !steam.Available || string.IsNullOrEmpty(steam.SteamId)) return null;
                return Profiles.Book.profiles.FirstOrDefault(p => p.steamId == steam.SteamId);
            }
        }

        /// <summary>Профиль изменился — отправить чуть позже (несколько сохранений подряд уйдут одним запросом).</summary>
        public static void MarkDirty() => Schedule.MarkDirty(Now);

        /// <summary>Каждый кадр из приложения: отложенная и периодическая синхронизация.</summary>
        public static void Tick()
        {
            EnsureSteamAccount();
            float now = Now;
            if (!_started)
            {
                _started = true;
                Schedule.Reset(now);
            }
            if (Schedule.TicketExpired(now))
            {
                Fail(Schedule.Token, "Steam auth ticket timed out.");
            }
            if (Schedule.Due(now)) SyncNow();
        }

        static bool Current(int epoch, string owner, int token) =>
            _accountEpoch == epoch && CurrentSteamId() == owner && Schedule.IsCurrent(token);

        static void Fail(int token, string problem)
        {
            if (!Schedule.Fail(token, Now)) return;
            Problem = problem;
            Changed?.Invoke();
        }

        public static void SyncNow()
        {
            EnsureSteamAccount();
            var profile = Target;
            var steam = NetPlatforms.Steam;
            if (profile == null || steam == null)
            {
                Schedule.Defer(Now);
                return;
            }
            if (!Schedule.TryBegin(Now, out int token)) return;
            string owner = _observedSteamId;
            int epoch = _accountEpoch;
            try
            {
                steam.RequestWebApiTicket(TicketIdentity, ticket =>
                {
                    EnsureSteamAccount();
                    if (!Current(epoch, owner, token) || !Schedule.WaitingForTicket) return;
                    if (string.IsNullOrWhiteSpace(ticket))
                    {
                        Fail(token, "Steam auth ticket unavailable.");
                        return;
                    }
                    if (!ReferenceEquals(Target, profile))
                    {
                        Fail(token, "Steam profile changed during sync.");
                        return;
                    }
                    // Persist all pending receipts and progress before sending them.
                    if (!Profiles.TrySaveLocal())
                    {
                        Fail(token, "Local profile could not be saved.");
                        return;
                    }
                    if (!Schedule.BeginHttp(token)) return;
                    try { Send(profile, owner, ticket, epoch, token); }
                    catch (Exception e) { Fail(token, e.Message); }
                });
            }
            catch (Exception e)
            {
                Fail(token, e.Message);
            }
        }

        static void Send(PlayerProfile profile, string steamId, string ticket, int epoch, int token)
        {
            var request = new SyncRequest
            {
                platform = "steam",
                ticket = ticket,
                claimedId = steamId,
                version = Application.version,
                applied = (profile.appliedActions ?? new List<long>()).ToArray(),
                profile = profile
            };
            var body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
            var web = new UnityWebRequest(Url + Function, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 20
            };
            web.SetRequestHeader("Content-Type", "application/json");
            web.SetRequestHeader("apikey", PublishableKey);
            UnityWebRequestAsyncOperation operation;
            try { operation = web.SendWebRequest(); }
            catch
            {
                web.Dispose();
                throw;
            }
            operation.completed += _ =>
            {
                EnsureSteamAccount();
                if (!Current(epoch, steamId, token))
                {
                    web.Dispose();
                    return;
                }
                try
                {
                    var text = web.downloadHandler?.text ?? "";
                    var response = string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<SyncResponse>(text);
                    if (web.result != UnityWebRequest.Result.Success || response == null || !string.IsNullOrEmpty(response.error))
                    {
                        Fail(token, response?.error ?? web.error ?? "No profile sync response.");
                        return;
                    }
                    if (!string.Equals(response.id, "steam:" + steamId, StringComparison.Ordinal) ||
                        !string.Equals(response.platformUserId, steamId, StringComparison.Ordinal))
                    {
                        Fail(token, "Steam profile sync identity mismatch");
                        return;
                    }
                    if (!ReferenceEquals(Target, profile))
                    {
                        Fail(token, "Steam profile changed during sync.");
                        return;
                    }
                    if (!Apply(profile, response))
                    {
                        Fail(token, "Local profile could not be saved.");
                        return;
                    }
                    bool notify = Problem.Length > 0 || ServerId != response.id;
                    Problem = "";
                    ServerId = response.id;
                    Schedule.Complete(token, Now);
                    if (notify) Changed?.Invoke();
                }
                catch (Exception e)
                {
                    Fail(token, e.Message);
                }
                finally
                {
                    web.Dispose();
                }
            };
        }

        static bool Apply(PlayerProfile profile, SyncResponse response)
        {
            // Invalid response entries must never clear the pending receipt set.
            if (response.actions == null || response.actions.Any(a => a == null || a.id <= 0))
                throw new InvalidOperationException("Invalid admin action response.");
            bool changed = profile.cloudBanned != response.banned || profile.cloudBanReason != (response.banReason ?? "");
            profile.cloudBanned = response.banned;
            profile.cloudBanReason = response.banReason ?? "";
            if (profile.appliedActions == null) profile.appliedActions = new List<long>();
            // Сервер вернул только неприменённые: подтверждённые им номера можно забыть.
            var pending = new HashSet<long>((response.actions ?? Array.Empty<AdminAction>()).Select(a => a.id));
            bool receiptsChanged = profile.appliedActions.RemoveAll(id => !pending.Contains(id)) > 0;
            bool appliedNew = false;
            foreach (var action in response.actions ?? Array.Empty<AdminAction>())
            {
                if (profile.appliedActions.Contains(action.id)) continue; // уже применено, ждёт подтверждения
                ApplyAction(profile, action);
                profile.appliedActions.Add(action.id);
                appliedNew = true;
            }
            if (changed || appliedNew || receiptsChanged)
            {
                // Keep in-memory receipts after an I/O error; the next attempt
                // must persist them before it can acknowledge them to the server.
                if (!Profiles.TrySaveLocal()) return false;
                Changed?.Invoke();
            }
            // A failed local save leaves receipts in memory; any outstanding
            // IDs still need acknowledgement after the next successful attempt.
            if (profile.appliedActions.Count > 0) Schedule.MarkDirty(Now, 1f);
            return true;
        }

        static void ApplyAction(PlayerProfile profile, AdminAction action)
        {
            string note = string.IsNullOrWhiteSpace(action.note) ? "" : action.note.Trim();
            switch (action.kind)
            {
                case "grant_influence":
                    profile.wallet = Math.Max(0, profile.wallet + action.amount);
                    if (action.amount > 0) profile.earnedTotal += action.amount;
                    Profiles.Record(profile, action.amount, "admin", note.Length > 0 ? note : "Начисление от разработчика");
                    Messages.Add(action.amount >= 0 ? $"Разработчик начислил {action.amount} влияния." : $"Разработчик списал {-action.amount} влияния.");
                    break;
                case "set_wallet":
                    int delta = Math.Max(0, action.amount) - profile.wallet;
                    profile.wallet = Math.Max(0, action.amount);
                    Profiles.Record(profile, delta, "admin", note.Length > 0 ? note : "Кошелёк изменён разработчиком");
                    break;
                case "grant_item":
                    if (!string.IsNullOrEmpty(action.item) && !profile.owned.Contains(action.item))
                    {
                        profile.owned.Add(action.item);
                        Messages.Add($"Подарок от разработчика: {ItemName(action.item)}.");
                    }
                    break;
                case "revoke_item":
                    if (!string.IsNullOrEmpty(action.item))
                    {
                        profile.owned.Remove(action.item);
                        // Надетое — снять.
                        if (profile.emblem == action.item) profile.emblem = "";
                        if (profile.title == action.item) profile.title = "";
                        if (profile.frame == action.item) profile.frame = "";
                        var clothing = CharacterCatalog.Item(action.item);
                        if (clothing != null && profile.Outfit.Get(clothing.slot) == action.item)
                        {
                            var fallback = CharacterCatalog.IsRequired(clothing.slot) ? CharacterCatalog.DefaultFor(clothing.slot, profile.Outfit.Gender) : "";
                            profile.outfit = profile.Outfit.With(clothing.slot, fallback ?? "").Serialize();
                        }
                    }
                    break;
            }
            if (action.kind == "message" || (note.Length > 0 && action.kind != "grant_influence" && action.kind != "set_wallet"))
                Messages.Add(note.Length > 0 ? note : "Сообщение от разработчика.");
        }

        static string ItemName(string id) => Profiles.Item(id)?.Name ?? CharacterCatalog.Item(id)?.name ?? id;
    }
}
