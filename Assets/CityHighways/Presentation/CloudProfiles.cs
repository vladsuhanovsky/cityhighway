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
        const float SaveDelay = 8f, Period = 300f;

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

        static bool _busy, _started;
        static float _nextAt = -1f, _lastSync;
        static string _observedSteamId;
        static int _accountEpoch;

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
            _busy = false;
            ServerId = "";
            Problem = "";
            Messages.Clear();
            _nextAt = Time.realtimeSinceStartup + 1f;
            _lastSync = Time.realtimeSinceStartup;
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
        public static void MarkDirty()
        {
            float at = Time.realtimeSinceStartup + SaveDelay;
            if (_nextAt < 0f || at < _nextAt || _nextAt <= Time.realtimeSinceStartup) _nextAt = at;
        }

        /// <summary>Каждый кадр из приложения: отложенная и периодическая синхронизация.</summary>
        public static void Tick()
        {
            EnsureSteamAccount();
            float now = Time.realtimeSinceStartup;
            if (!_started)
            {
                _started = true;
                _nextAt = now + 1f; // первая — сразу после запуска (Steam успеет подняться)
            }
            if (_busy) return;
            if ((_nextAt >= 0f && now >= _nextAt) || now - _lastSync > Period) SyncNow();
        }

        public static void SyncNow()
        {
            EnsureSteamAccount();
            if (_busy) return;
            _nextAt = -1f;
            _lastSync = Time.realtimeSinceStartup;
            var profile = Target;
            var steam = NetPlatforms.Steam;
            if (profile == null || steam == null) return;
            string owner = _observedSteamId;
            int epoch = _accountEpoch;
            _busy = true;
            steam.RequestWebApiTicket(TicketIdentity, ticket =>
            {
                EnsureSteamAccount();
                if (_accountEpoch != epoch || CurrentSteamId() != owner) return;
                try
                {
                    Send(profile, owner, ticket ?? "", epoch);
                }
                catch (Exception e)
                {
                    if (_accountEpoch != epoch) return;
                    _busy = false;
                    Problem = e.Message;
                }
            });
        }

        static void Send(PlayerProfile profile, string steamId, string ticket, int epoch)
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
            web.SendWebRequest().completed += _ =>
            {
                EnsureSteamAccount();
                if (_accountEpoch != epoch || CurrentSteamId() != steamId)
                {
                    web.Dispose();
                    return;
                }
                _busy = false;
                try
                {
                    var text = web.downloadHandler?.text ?? "";
                    var response = string.IsNullOrEmpty(text) ? null : JsonUtility.FromJson<SyncResponse>(text);
                    if (web.result != UnityWebRequest.Result.Success || response == null || !string.IsNullOrEmpty(response.error))
                    {
                        Problem = response?.error ?? web.error ?? "нет ответа";
                        return;
                    }
                    if (!string.Equals(response.id, "steam:" + steamId, StringComparison.Ordinal) ||
                        !string.Equals(response.platformUserId, steamId, StringComparison.Ordinal))
                    {
                        Problem = "Steam profile sync identity mismatch";
                        return;
                    }
                    Problem = "";
                    ServerId = response.id;
                    Apply(profile, response);
                }
                catch (Exception e)
                {
                    Problem = e.Message;
                }
                finally
                {
                    web.Dispose();
                }
            };
        }

        static void Apply(PlayerProfile profile, SyncResponse response)
        {
            bool changed = profile.cloudBanned != response.banned || profile.cloudBanReason != (response.banReason ?? "");
            profile.cloudBanned = response.banned;
            profile.cloudBanReason = response.banReason ?? "";
            if (profile.appliedActions == null) profile.appliedActions = new List<long>();
            // Сервер вернул только неприменённые: подтверждённые им номера можно забыть.
            var pending = new HashSet<long>((response.actions ?? Array.Empty<AdminAction>()).Select(a => a.id));
            profile.appliedActions.RemoveAll(id => !pending.Contains(id));
            bool appliedNew = false;
            foreach (var action in response.actions ?? Array.Empty<AdminAction>())
            {
                if (profile.appliedActions.Contains(action.id)) continue; // уже применено, ждёт подтверждения
                ApplyAction(profile, action);
                profile.appliedActions.Add(action.id);
                appliedNew = true;
            }
            if (changed || appliedNew)
            {
                Profiles.SaveLocal();
                Changed?.Invoke();
            }
            // Сообщить серверу о применённых — следующей синхронизацией, не откладывая.
            if (appliedNew) _nextAt = Time.realtimeSinceStartup + 1f;
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
