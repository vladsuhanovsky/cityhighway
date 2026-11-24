using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using CityHighways.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Социальная часть через облако (Supabase, функция social): рекорды друзей (призраки и рейды), повторы их партий,
    /// клубы строителей. Вход — тем же билетом Steam, что и синхронизация профиля (CloudProfiles): сервер сам узнаёт
    /// SteamID, подставить чужой нельзя. Без Steam облако молчит, а призраки берутся из других профилей на этом
    /// компьютере (Ghosts).
    /// </summary>
    public static partial class SocialCloud
    {
        const string Function = "/functions/v1/social";
        const string TicketIdentity = "cityhighways";
        const float FriendsPeriod = 180f, ClubPeriod = 120f;

        // ---------------------------------------------------------------- Данные

        [Serializable]
        public class RecordInfo
        {
            public long id;
            public string playerId;
            public string name;
            public string mode;
            public string mapPath;
            public string mapName;
            public int score;
            public bool won;
            public int seats;
            public string date;
        }

        [Serializable]
        public class ClubInfo
        {
            public string id;
            public string name;
            public string tag;
            public string code;
            public bool open;
            public string logo;
            public bool owner;
        }

        [Serializable]
        public class MemberInfo
        {
            public string name;
            public string role;
            public bool me;
            public int amount;
            public int games;
        }

        [Serializable]
        public class WeekInfo
        {
            public string id;
            public string endsAt;
            public string kind;
            public int target;
            public int progress;
            public bool completed;
        }

        [Serializable]
        public class LogoInfo
        {
            public string logo;
            public string week;
        }

        [Serializable]
        public class ClubState
        {
            public ClubInfo club;
            public MemberInfo[] members;
            public WeekInfo week;
            public int weeksDone;
            public LogoInfo[] logos;
            public RecordInfo[] records;
            public string error;

            public bool HasClub => club != null && !string.IsNullOrEmpty(club.id);
        }

        [Serializable]
        public class ClubBrief
        {
            public string id;
            public string name;
            public string tag;
            public string logo;
            public int members;
        }

        [Serializable]
        class ClubListResponse
        {
            public ClubBrief[] clubs;
            public string error;
        }

        [Serializable]
        class FriendsResponse
        {
            public RecordInfo[] records;
            public string error;
        }

        [Serializable]
        class ReplayResponse
        {
            public string replay;
            public string author;
            public string error;
        }

        [Serializable]
        public class ReportClub
        {
            public string kind;
            public int amount;
            public bool closed;
            public string logo;
        }

        [Serializable]
        public class ReportResponse
        {
            public bool improved;
            public int best;
            public ReportClub club;
            public string error;
        }

        [Serializable]
        class ReportStats
        {
            public int roadPieces;
            public int contracts;
            public int coopOrders;
            public int delivered;
            public bool won;
        }

        /// <summary>Один плоский запрос на все действия (JsonUtility не умеет «лишних» полей — сервер их пропускает).</summary>
        [Serializable]
        class Request
        {
            public string platform = "steam";
            public string ticket;
            public string claimedId;
            public string action;
            public string version;
            // report
            public string mode;
            public string mapPath;
            public string mapName;
            public int score;
            public bool won;
            public int seats;
            public int seed;
            public string replay;
            /// <summary>One Magnate match, reused for any HTTP retry.</summary>
            public string runId;
            public ReportStats stats;
            // friends
            public string[] ids;
            // replay
            public long recordId;
            // клубы
            public string name;
            public string tag;
            public string logo;
            public string code;
            public string clubId;
            public string query;
            public bool open = true;
        }

        /// <summary>Рекорды друзей Steam (все карты и режимы), последняя загрузка.</summary>
        public static readonly List<RecordInfo> FriendRecords = new List<RecordInfo>();
        /// <summary>Свой клуб; null — ещё не загружен, HasClub = false — не состоит.</summary>
        public static ClubState Club { get; private set; }
        public static string Problem { get; private set; } = "";
        /// <summary>Что-то обновилось (друзья, клуб, итог отчёта) — страницы перерисовываются.</summary>
        public static event Action Changed;
        /// <summary>Сообщение после партии («Рекорд на карте обновлён», «Клуб выполнил цель недели!»).</summary>
        public static readonly List<string> News = new List<string>();

        static float _friendsAt = -999f, _clubAt = -999f;
        static bool _friendsBusy, _clubBusy;

        /// <summary>Облако доступно: Steam запущен, профиль уже синхронизирован с сервером.</summary>
        public static bool Available
        {
            get
            {
                var steam = NetPlatforms.Steam;
                return steam != null && steam.Available && !string.IsNullOrEmpty(steam.SteamId) && !string.IsNullOrEmpty(CloudProfiles.ServerId)
                       && !(Profiles.Active?.cloudBanned ?? false);
            }
        }

        /// <summary>Почему облако недоступно — для подсказки на страницах.</summary>
        public static string Unavailable
        {
            get
            {
                var steam = NetPlatforms.Steam;
                if (steam == null || !steam.Available) return LocalizationService.Language == "en" ? "Friends’ records and clubs require Steam: launch the game through Steam." : "Рекорды друзей и клубы работают через Steam: запустите игру из Steam.";
                if (Profiles.Active?.cloudBanned ?? false) return LocalizationService.Language == "en" ? "The account has been blocked by an administrator." : "Аккаунт заблокирован администратором.";
                if (string.IsNullOrEmpty(CloudProfiles.ServerId))
                    return string.IsNullOrEmpty(CloudProfiles.Problem)
                        ? (LocalizationService.Language == "en" ? "Your profile is still syncing with the server…" : "Профиль ещё синхронизируется с сервером…")
                        : (LocalizationService.Language == "en" ? "Cannot connect to the server: " : "Нет связи с сервером: ") + CloudProfiles.Problem;
                return "";
            }
        }

        /// <summary>Каждый кадр: подтянуть друзей и клуб, когда облако стало доступно, и обновлять раз в несколько минут.</summary>
        public static void Tick()
        {
            if (!Available) return;
            float now = Time.realtimeSinceStartup;
            if (!_friendsBusy && now - _friendsAt > FriendsPeriod) RefreshFriends();
            if (!_clubBusy && now - _clubAt > ClubPeriod) RefreshClub();
        }

        // ---------------------------------------------------------------- Транспорт

        static void Send<T>(Request request, Action<T> done, Action<string> failed = null,
            Action<string, long> reportFailure = null, string expectedSteamId = null) where T : class
        {
            var steam = NetPlatforms.Steam;
            if (!Available || steam == null)
            {
                failed?.Invoke(Unavailable);
                reportFailure?.Invoke(Unavailable, 0);
                return;
            }
            steam.RequestWebApiTicket(TicketIdentity, ticket =>
            {
                try
                {
                    // If Steam account changes during async ticket acquisition,
                    // do NOT publish a queued report as the newly signed-in user.
                    if (expectedSteamId != null && steam.SteamId != expectedSteamId)
                    {
                        reportFailure?.Invoke("Steam account changed", 0);
                        return;
                    }
                    request.ticket = ticket ?? "";
                    request.claimedId = steam.SteamId;
                    request.version = Application.version;
                    var body = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
                    var web = new UnityWebRequest(CloudProfiles.Url + Function, UnityWebRequest.kHttpVerbPOST)
                    {
                        uploadHandler = new UploadHandlerRaw(body),
                        downloadHandler = new DownloadHandlerBuffer(),
                        timeout = 25
                    };
                    web.SetRequestHeader("Content-Type", "application/json");
                    web.SetRequestHeader("apikey", CloudProfiles.PublishableKey);
                    web.SendWebRequest().completed += _ =>
                    {
                        try
                        {
                            string text = web.downloadHandler?.text ?? "";
                            string error = ErrorOf(text);
                            if (web.result != UnityWebRequest.Result.Success || !string.IsNullOrEmpty(error))
                            {
                                Problem = Russian(error ?? web.error ?? "нет ответа");
                                failed?.Invoke(Problem);
                                reportFailure?.Invoke(Problem, web.responseCode);
                                return;
                            }
                            Problem = "";
                            done?.Invoke(JsonUtility.FromJson<T>(text));
                        }
                        catch (Exception e)
                        {
                            Problem = e.Message;
                            failed?.Invoke(e.Message);
                            reportFailure?.Invoke(e.Message, 0);
                        }
                        finally
                        {
                            web.Dispose();
                        }
                    };
                }
                catch (Exception e)
                {
                    Problem = e.Message;
                    failed?.Invoke(e.Message);
                    reportFailure?.Invoke(e.Message, 0);
                }
            });
        }

        [Serializable]
        class ErrorOnly
        {
            public string error;
        }

        static string ErrorOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                var e = JsonUtility.FromJson<ErrorOnly>(text);
                return string.IsNullOrEmpty(e?.error) ? null : e.error;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Ошибки сервера — по-русски.</summary>
        public static string Russian(string error)
        {
            switch (error)
            {
                case "already in a club": return "Вы уже состоите в клубе — сначала выйдите из него.";
                case "name too short": return "Название клуба — от 3 символов.";
                case "bad tag": return "Тег — 2–4 буквы или цифры.";
                case "name taken": return "Клуб с таким названием уже есть.";
                case "club not found": return "Клуб не найден — проверьте код.";
                case "club is closed": return "Клуб закрыт: вступить можно только по коду приглашения.";
                case "club is full": return "В клубе уже 30 участников.";
                case "owner only": return "Это может только владелец клуба.";
                case "logo not earned": return "Этот логотип клуб ещё не заработал.";
                case "replay not found": return "Повтор не найден.";
                case "profile not synced yet": return "Профиль ещё не синхронизирован с сервером — попробуйте через минуту.";
                case "banned": return "Аккаунт заблокирован.";
                case "steam ticket rejected": return "Steam не подтвердил вход.";
                case "steam verification is not configured": return "Сервер ещё не настроен для проверки Steam.";
                default: return error;
            }
        }

        // ---------------------------------------------------------------- Друзья и рекорды

        /// <summary>SteamID друзей из клиента Steam.</summary>
        static string[] FriendIds()
        {
            var steam = NetPlatforms.Steam;
            if (steam == null || !steam.Available) return new string[0];
            return steam.Friends().Select(f => f.SteamId).Where(id => !string.IsNullOrEmpty(id)).Distinct().Take(400).ToArray();
        }

        public static void RefreshFriends(Action done = null)
        {
            _friendsAt = Time.realtimeSinceStartup;
            var ids = FriendIds();
            if (ids.Length == 0)
            {
                FriendRecords.Clear();
                done?.Invoke();
                return;
            }
            _friendsBusy = true;
            Send<FriendsResponse>(new Request { action = "friends", ids = ids }, response =>
            {
                _friendsBusy = false;
                FriendRecords.Clear();
                if (response?.records != null) FriendRecords.AddRange(response.records);
                Changed?.Invoke();
                done?.Invoke();
            }, _ =>
            {
                _friendsBusy = false;
                done?.Invoke();
            });
        }

        /// <summary>Скачать повтор рекорда (друга или клубного товарища) и сразу открыть его.</summary>
        public static void OpenRecordReplay(RecordInfo record, Action<string> failed = null)
        {
            if (record == null) return;
            FetchReplay(record, replay => Replays.Play?.Invoke(replay), failed);
        }

        public static void FetchReplay(RecordInfo record, Action<ReplayFile> done, Action<string> failed = null)
        {
            Send<ReplayResponse>(new Request { action = "replay", recordId = record.id }, response =>
            {
                var replay = Replays.Unpack(response?.replay);
                if (replay == null)
                {
                    failed?.Invoke("Повтор не открылся.");
                    return;
                }
                replay.source = "friend";
                if (!string.IsNullOrEmpty(response.author)) replay.author = response.author;
                done?.Invoke(replay);
            }, failed);
        }

        /// <summary>
        /// Итог партии на сервер: рекорд карты (с повтором — если он лучше прежнего) и вклад в цель недели клуба.
        /// Обучение, партии без человека и партии без облака не отправляются.
        /// </summary>
        public static void ReportGame(Game game, GameSetup setup, ReplayFile replay, Ghost ghost)
        {
            if (!Available || game?.Result == null || setup == null || setup.tutorial || setup.practice || replay == null) return;
            int seat = replay.authorSeat;
            if (seat < 0 || seat >= game.Players.Count || replay.humans == null || !replay.humans[seat]) return;
            var player = game.Players[seat];
            var score = game.Result.Ranking.FirstOrDefault(s => s.PlayerIndex == seat);
            var stats = new ReportStats
            {
                roadPieces = player.Roads.Sum(r => r.Length),
                contracts = score?.CompletedContracts ?? 0,
                coopOrders = game.IsCooperative ? game.OrdersDone : 0,
                won = replay.won
            };
            var request = new Request
            {
                action = "report",
                mode = replay.mode,
                mapPath = replay.mapPath,
                mapName = replay.map,
                score = replay.authorScore,
                won = replay.won,
                seats = replay.players?.Length ?? 0,
                seed = replay.start?.seed ?? 0,
                replay = Replays.Pack(replay),
                stats = stats
            };
            Send<ReportResponse>(request, AfterReport);
        }

        /// <summary>Магнат: рекордов и повторов нет, но доставки и победы идут в цель клуба.</summary>
        public static void ReportMagnate(LogisticsGame game, int humanIndex)
        {
            // Never send a provisional score or a bot-owned result.
            // The run ID belongs to the finished game object and survives
            // retransmission of the same report within this session.
            if (!Available || game == null || game.Phase != MagnatePhase.GameOver ||
                humanIndex < 0 || humanIndex >= game.Players.Count ||
                game.Players[humanIndex].IsBot ||
                !MagnateRunIdentity.IsValid(game.ReportRunId)) return;
            var player = game.Players[humanIndex];
            int best = game.Players.Max(p => p.Score);
            Send<ReportResponse>(new Request
            {
                action = "report",
                mode = "magnate",
                runId = game.ReportRunId,
                mapName = game.Map.Name,
                score = player.Score,
                won = player.Score == best,
                stats = new ReportStats { delivered = player.Delivered, won = player.Score == best }
            }, AfterReport);
        }

        static void AfterReport(ReportResponse response)
        {
            if (response == null) return;
            if (response.improved) News.Add("Новый личный рекорд на карте отправлен друзьям.");
            if (response.club != null && response.club.closed)
                News.Add("Клуб выполнил цель недели! Новый клубный логотип ждёт на странице «Клубы и друзья».");
            else if (response.club != null && response.club.amount > 0)
                News.Add($"Вклад в цель клуба: +{response.club.amount} {GoalUnit(response.club.kind, response.club.amount)}.");
            RefreshClub();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- Клубы

        public static void RefreshClub(Action done = null)
        {
            _clubAt = Time.realtimeSinceStartup;
            _clubBusy = true;
            Send<ClubState>(new Request { action = "club" }, state =>
            {
                _clubBusy = false;
                Club = state;
                Changed?.Invoke();
                done?.Invoke();
            }, _ =>
            {
                _clubBusy = false;
                Changed?.Invoke(); // страница покажет, что не загрузилось
                done?.Invoke();
            });
        }

        static void ClubAction(Request request, Action<string> failed)
        {
            Send<ClubState>(request, state =>
            {
                Club = state;
                _clubAt = Time.realtimeSinceStartup;
                Changed?.Invoke();
            }, failed);
        }

        public static void CreateClub(string name, string tag, string logo, bool open, Action<string> failed) =>
            ClubAction(new Request { action = "club_create", name = name, tag = tag, logo = logo, open = open }, failed);

        public static void JoinClub(string code, string clubId, Action<string> failed) =>
            ClubAction(new Request { action = "club_join", code = code ?? "", clubId = clubId ?? "" }, failed);

        public static void LeaveClub(Action<string> failed) => ClubAction(new Request { action = "club_leave" }, failed);

        public static void SetClubLogo(string logo, Action<string> failed) => ClubAction(new Request { action = "club_logo", logo = logo }, failed);

        public static void SetClubOpen(bool open, Action<string> failed) => ClubAction(new Request { action = "club_open", open = open }, failed);

        public static void ListClubs(string query, Action<ClubBrief[]> done, Action<string> failed) =>
            Send<ClubListResponse>(new Request { action = "club_list", query = query ?? "" }, r => done?.Invoke(r?.clubs ?? new ClubBrief[0]), failed);

        /// <summary>Тег и герб своего клуба — для лобби и карточки профиля (пусто — не в клубе).</summary>
        public static string ClubTag => Club != null && Club.HasClub ? Club.club.tag : "";
        public static string ClubLogoId => Club != null && Club.HasClub ? Club.club.logo : "";

        // ---------------------------------------------------------------- Тексты целей

        public static string GoalTitle(string kind, int target)
        {
            switch (kind)
            {
                case "roads": return $"Построить {target} км дорог";
                case "contracts": return $"Выполнить {target} контрактов";
                case "orders": return $"Выполнить {target} заказов города в кооперативе";
                case "wins": return $"Одержать {target} побед";
                case "cargo": return $"Доставить {target} грузов в «Логистическом магнате»";
                default: return $"Цель недели: {target}";
            }
        }

        public static string GoalUnit(string kind, int amount)
        {
            switch (kind)
            {
                case "roads": return "км";
                case "contracts": return "контр.";
                case "orders": return "заказ.";
                case "wins": return amount == 1 ? "победа" : "побед";
                case "cargo": return "груз.";
                default: return "";
            }
        }

        public static string GoalHint(string kind)
        {
            switch (kind)
            {
                case "roads": return "Каждый построенный участок дороги — 10 км. Считаются партии соревнования и кооператива.";
                case "contracts": return "Считаются выполненные контракты в соревновании и кооперативе.";
                case "orders": return "Считаются выполненные заказы города в кооперативе — вся команда, а не только ваша дорога.";
                case "wins": return "Победы в любом режиме; в кооперативе — победа команды.";
                case "cargo": return "Доставленные грузы в «Логистическом магнате».";
                default: return "";
            }
        }
    }
}
