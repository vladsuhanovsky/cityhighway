using System;
using System.Linq;
using CityHighways.Core;
using CityHighways.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace CityHighways.Presentation
{
    /// <summary>
    /// Страница «Сетевая игра»: создать лобби или подключиться (Steam или локальная сеть), затем лобби —
    /// кто за столом, боты, таймер хода и кнопка «Начать» у хоста.
    /// </summary>
    public sealed class MenuOnlinePage
    {
        readonly CityHighwaysApp _app;
        readonly GameSetup _setup;
        readonly Action _back;

        readonly VisualElement _connectView;
        readonly VisualElement _lobbyView;
        readonly TextField _nameField;
        readonly TextField _addressField;
        readonly Label _status;
        readonly VisualElement _friends;
        readonly TextField _codeField;
        readonly VisualElement _openLobbies;

        readonly Label _lobbyTitle;
        readonly Label _lobbyHint;
        readonly VisualElement _seatsList;
        readonly VisualElement _hostControls;
        readonly Label _waitLabel;
        readonly UnityEngine.UIElements.Button _inviteButton;
        readonly UnityEngine.UIElements.Button _addBotButton;
        readonly UnityEngine.UIElements.Button _timerButton;
        readonly UnityEngine.UIElements.Button _startButton;

        NetSession _session;
        int _turnSeconds;

        public VisualElement Root { get; }

        public MenuOnlinePage(CityHighwaysApp app, GameSetup setup, Action back)
        {
            _app = app;
            _setup = setup;
            _back = back;
            _turnSeconds = setup.turnSeconds > 0 ? setup.turnSeconds : 120;

            Root = Ui.Column();

            // ------------------------------------------------------------ Подключение
            _connectView = Ui.Column();
            Root.Add(_connectView);
            _connectView.Add(Title(LocalizationService.Get("menu.online.001"), Leave));

            var nameRow = Ui.Row();
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 14f;
            nameRow.Add(Ui.Text(LocalizationService.Get("menu.online.002"), 16f, Theme.TextDim));
            _nameField = Field(DefaultName(), 16);
            _nameField.style.width = InterfaceLayout.OnlineNameFieldWidth;
            _nameField.style.marginLeft = 14f;
            nameRow.Add(_nameField);
            _connectView.Add(nameRow);

            var columns = Ui.Row();
            columns.style.alignItems = Align.FlexStart;
            _connectView.Add(columns);

            // Unity Relay + Lobby — через интернет из любых сетей.
            var ugsCard = Ui.Card(18f);
            ugsCard.style.flexGrow = 1f;
            ugsCard.style.flexBasis = 0f;
            ugsCard.style.marginRight = 14f;
            ugsCard.Add(Ui.Text(LocalizationService.Get("menu.online.003"), 22f, Theme.Gold, true));
            if (NetPlatforms.Ugs == null)
            {
                ugsCard.Add(Note(LocalizationService.Get("menu.online.004")));
            }
            else
            {
                ugsCard.Add(Note(LocalizationService.Get("menu.online.005")));
                var hostUgs = Ui.Button(LocalizationService.Get("menu.online.006"), HostUgs, ButtonKind.Primary, 17f);
                hostUgs.style.marginTop = 10f;
                ugsCard.Add(hostUgs);
                var codeRow = Ui.Row();
                codeRow.style.alignItems = Align.Center;
                codeRow.style.marginTop = 14f;
                _codeField = Field("", 12);
                _codeField.style.flexGrow = 1f;
                codeRow.Add(_codeField);
                var byCode = Ui.Button(LocalizationService.Get("menu.online.007"), () => JoinUgs(_codeField.value, true), ButtonKind.Secondary, 15f);
                byCode.style.marginLeft = 10f;
                codeRow.Add(byCode);
                ugsCard.Add(codeRow);
                var listHeader = Ui.Row();
                listHeader.style.alignItems = Align.Center;
                listHeader.style.marginTop = 16f;
                var listTitle = Ui.Text(LocalizationService.Get("menu.online.008"), 15f, Theme.Text, true);
                listTitle.style.flexGrow = 1f;
                listHeader.Add(listTitle);
                listHeader.Add(Ui.Button(LocalizationService.Get("menu.online.009"), RefreshOpenLobbies, ButtonKind.Ghost, 13f));
                ugsCard.Add(listHeader);
                _openLobbies = Ui.Column();
                _openLobbies.style.marginTop = 6f;
                _openLobbies.Add(Note(LocalizationService.Get("menu.online.010")));
                ugsCard.Add(_openLobbies);
            }
            columns.Add(ugsCard);

            // Steam
            var steamCard = Ui.Card(18f);
            steamCard.style.flexGrow = 1f;
            steamCard.style.flexBasis = 0f;
            steamCard.style.marginRight = 14f;
            steamCard.Add(Ui.Text("Steam", 22f, Theme.Gold, true));
            var steam = NetPlatforms.Steam;
            if (steam == null)
            {
                steamCard.Add(Note(LocalizationService.Get("menu.online.011")));
            }
            else if (!steam.Available)
            {
                steamCard.Add(Note(steam.Problem));
            }
            else
            {
                steamCard.Add(Note(LocalizationService.Get("menu.online.012")));
                var host = Ui.Button(LocalizationService.Get("menu.online.013"), HostSteam, ButtonKind.Primary, 17f);
                host.style.marginTop = 10f;
                steamCard.Add(host);
                var friendsHeader = Ui.Row();
                friendsHeader.style.alignItems = Align.Center;
                friendsHeader.style.marginTop = 16f;
                var friendsTitle = Ui.Text(LocalizationService.Get("menu.online.014"), 15f, Theme.Text, true);
                friendsTitle.style.flexGrow = 1f;
                friendsHeader.Add(friendsTitle);
                friendsHeader.Add(Ui.Button(LocalizationService.Get("menu.online.009"), RefreshFriends, ButtonKind.Ghost, 13f));
                steamCard.Add(friendsHeader);
                _friends = Ui.Column();
                _friends.style.marginTop = 6f;
                steamCard.Add(_friends);
            }
            columns.Add(steamCard);

            // Локальная сеть
            var lanCard = Ui.Card(18f);
            lanCard.style.flexGrow = 1f;
            lanCard.style.flexBasis = 0f;
            lanCard.Add(Ui.Text(LocalizationService.Get("menu.online.015"), 22f, Theme.Gold, true));
            lanCard.Add(Note(string.Format(LocalizationService.Get("menu.online.dynamic.lan_help"), TcpTransport.DefaultPort)));
            var hostLan = Ui.Button(LocalizationService.Get("menu.online.016"), HostLan, ButtonKind.Primary, 17f);
            hostLan.style.marginTop = 10f;
            lanCard.Add(hostLan);
            var joinRow = Ui.Row();
            joinRow.style.alignItems = Align.Center;
            joinRow.style.marginTop = 16f;
            _addressField = Field(PlayerPrefs.GetString(AddressKey, "127.0.0.1"), 64);
            _addressField.style.flexGrow = 1f;
            joinRow.Add(_addressField);
            var join = Ui.Button(LocalizationService.Get("menu.online.017"), JoinLan, ButtonKind.Secondary, 15f);
            join.style.marginLeft = 10f;
            joinRow.Add(join);
            lanCard.Add(joinRow);
            lanCard.Add(Note(LocalizationService.Get("menu.online.018")));
            columns.Add(lanCard);

            _status = Ui.Text("", 15f, Theme.Danger);
            _status.style.marginTop = 14f;
            _status.style.whiteSpace = WhiteSpace.Normal;
            _connectView.Add(_status);

            // ------------------------------------------------------------ Лобби
            _lobbyView = Ui.Column();
            Root.Add(_lobbyView);
            var lobbyHeader = Title(LocalizationService.Get("menu.online.019"), Leave);
            _lobbyTitle = lobbyHeader.Q<Label>();
            _lobbyView.Add(lobbyHeader);
            _lobbyHint = Note("");
            _lobbyHint.style.marginBottom = 10f;
            _lobbyView.Add(_lobbyHint);

            var seatsCard = Ui.Card(18f);
            seatsCard.Add(Ui.Text(LocalizationService.Get("menu.online.020"), 18f, Theme.Text, true));
            _seatsList = Ui.Column();
            _seatsList.style.marginTop = 10f;
            seatsCard.Add(_seatsList);
            _lobbyView.Add(seatsCard);

            _hostControls = Ui.Row();
            _hostControls.style.marginTop = 14f;
            _hostControls.style.flexWrap = Wrap.Wrap;
            _inviteButton = Ui.Button(LocalizationService.Get("menu.online.021"), () => _session?.Transport.Invite(), ButtonKind.Secondary, 15f);
            _addBotButton = Ui.Button(LocalizationService.Get("menu.online.022"), () => (_session as HostSession)?.AddBot(0), ButtonKind.Secondary, 15f);
            _timerButton = Ui.Button("", CycleTimer, ButtonKind.Secondary, 15f);
            _scenarioButton = Ui.Button("", CycleScenario, ButtonKind.Secondary, 15f);
            _mapButton = Ui.Button("", CycleMap, ButtonKind.Secondary, 15f);
            _modeButton = Ui.Button("", () => { _setup.cooperative = !_setup.cooperative; _setup.Save(); RefreshLobby(); }, ButtonKind.Secondary, 15f);
            _coopDifficultyButton = Ui.Button("", () =>
            {
                _setup.coopDifficulty = (int)CooperativeDifficulty.Next(_setup.coopDifficulty);
                _setup.Save();
                RefreshLobby();
            }, ButtonKind.Secondary, 15f);
            _startButton = Ui.Button(LocalizationService.Get("menu.online.023"), StartGame, ButtonKind.Primary, 17f);
            foreach (var b in new[] { _inviteButton, _addBotButton, _timerButton, _scenarioButton, _mapButton, _modeButton, _coopDifficultyButton })
            {
                b.style.marginRight = 10f;
                _hostControls.Add(b);
            }
            var spacer = Ui.Box();
            spacer.style.flexGrow = 1f;
            _hostControls.Add(spacer);
            _hostControls.Add(_startButton);
            _lobbyView.Add(_hostControls);

            _waitLabel = Ui.Text(LocalizationService.Get("menu.online.024"), 16f, Theme.TextDim);
            _waitLabel.style.marginTop = 14f;
            _lobbyView.Add(_waitLabel);

            ShowConnect("");
            RefreshFriends();
        }

        const string AddressKey = "CityHighways.Net.Address";

        string DefaultName()
        {
            var steam = NetPlatforms.Steam;
            if (steam != null && steam.Available && !string.IsNullOrEmpty(steam.PersonaName)) return steam.PersonaName;
            return Profiles.Active.name;
        }

        string PlayerName => string.IsNullOrWhiteSpace(_nameField.value) ? LocalizationService.Get("menu.online.025") : _nameField.value.Trim();

        // ================================================================== Подключение

        void HostLan() => Open(new HostSession(TcpTransport.Host(), CityHighwaysApp.LoadMap, PlayerName), LocalizationService.Get("menu.online.026"));

        ClientSession ClientFor(INetTransport transport, string destination)
        {
            var profile = Profiles.Active;
            var steam = NetPlatforms.Steam;
            string account = steam != null && steam.Available ? steam.SteamId : "";
            string key = _app.ReconnectCredentials.For(profile, account, destination);
            return new ClientSession(transport, CityHighwaysApp.LoadMap, PlayerName, reconnectKey: key);
        }

        void JoinLan()
        {
            PlayerPrefs.SetString(AddressKey, _addressField.value);
            Open(ClientFor(TcpTransport.Connect(_addressField.value), "lan:" + (_addressField.value ?? "").Trim().ToLowerInvariant()), LocalizationService.Get("menu.online.027"));
        }

        void HostUgs()
        {
            var transport = NetPlatforms.Ugs?.CreateHost($"{PlayerName} · City Highways", NetSession.MaxSeats);
            if (transport == null)
            {
                ShowConnect(LocalizationService.Get("menu.online.028"));
                return;
            }
            Open(new HostSession(transport, CityHighwaysApp.LoadMap, PlayerName), LocalizationService.Get("menu.online.029"));
        }

        void JoinUgs(string codeOrId, bool byCode)
        {
            if (byCode && string.IsNullOrWhiteSpace(codeOrId))
            {
                ShowConnect(LocalizationService.Get("menu.online.030"));
                return;
            }
            var transport = byCode ? NetPlatforms.Ugs?.JoinByCode(codeOrId) : NetPlatforms.Ugs?.JoinById(codeOrId);
            if (transport == null)
            {
                ShowConnect(LocalizationService.Get("menu.online.028"));
                return;
            }
            Open(ClientFor(transport, (byCode ? "ugs-code:" : "ugs-id:") + (codeOrId ?? "").Trim()), LocalizationService.Get("menu.online.031"));
        }

        void RefreshOpenLobbies()
        {
            if (_openLobbies == null || NetPlatforms.Ugs == null) return;
            _openLobbies.Clear();
            _openLobbies.Add(Note(LocalizationService.Get("menu.online.032")));
            NetPlatforms.Ugs.QueryLobbies((lobbies, error) =>
            {
                _openLobbies.Clear();
                if (error != null)
                {
                    _openLobbies.Add(Note(LocalizationService.Get("menu.online.033") + error));
                    return;
                }
                if (lobbies.Count == 0)
                {
                    _openLobbies.Add(Note(LocalizationService.Get("menu.online.034")));
                    return;
                }
                foreach (var lobby in lobbies)
                {
                    var row = Ui.Row();
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 6f;
                    var name = Ui.Text($"{lobby.Name}  ({lobby.Players}/{lobby.MaxPlayers})", 14f, Theme.Text);
                    name.style.flexGrow = 1f;
                    name.style.whiteSpace = WhiteSpace.Normal;
                    row.Add(name);
                    string id = lobby.Id;
                    row.Add(Ui.Button(LocalizationService.Get("menu.online.035"), () => JoinUgs(id, false), ButtonKind.Secondary, 14f));
                    _openLobbies.Add(row);
                }
            });
        }

        void HostSteam()
        {
            var transport = NetPlatforms.Steam?.CreateHost(NetSession.MaxSeats);
            if (transport == null)
            {
                ShowConnect(LocalizationService.Get("menu.online.036"));
                return;
            }
            Open(new HostSession(transport, CityHighwaysApp.LoadMap, PlayerName), LocalizationService.Get("menu.online.037"));
        }

        /// <summary>Подключиться к лобби друга (из списка, по приглашению или с +connect_lobby).</summary>
        public void JoinSteam(ulong lobby)
        {
            var transport = NetPlatforms.Steam?.Join(lobby);
            if (transport == null)
            {
                ShowConnect(LocalizationService.Get("menu.online.036"));
                return;
            }
            Open(ClientFor(transport, "steam:" + lobby), LocalizationService.Get("menu.online.038"));
        }

        void Open(NetSession session, string status)
        {
            // Оформление из активного профиля: хосту — на своё место, клиенту — в приветствие.
            var look = Profiles.Active.Look;
            if (session is HostSession && session.Seats.Count > 0)
            {
                session.Seats[0].emblem = look.Emblem;
                session.Seats[0].title = look.Title;
                session.Seats[0].frame = look.Frame;
                session.Seats[0].outfit = look.Outfit;
                session.Seats[0].clubTag = SocialCloud.ClubTag;
                session.Seats[0].clubLogo = SocialCloud.ClubLogoId;
            }
            else if (session is ClientSession client)
            {
                client.Emblem = look.Emblem;
                client.Title = look.Title;
                client.ClubTag = SocialCloud.ClubTag;
                client.ClubLogo = SocialCloud.ClubLogoId;
                client.Frame = look.Frame;
                client.Outfit = look.Outfit;
            }
            _app.OpenNet(session);
            _session = session;
            session.SeatsChanged += () => { if (_session == session) RefreshLobby(); };
            session.Closed += text =>
            {
                if (_session != session) return;
                _session = null;
                // Закрываем на следующем кадре: событие пришло изнутри опроса этого же канала.
                _app.Tweener.After(0.01f, () => { if (_app.Net == session) _app.CloseNet(); });
                ShowConnect(text);
            };
            session.Started += () =>
            {
                if (_session != session) return;
                _session = null; // дальше сессией владеет партия
                _app.StartOnlineGame();
            };
            session.Transport.Ready += () =>
            {
                if (_session == session && session.IsHost) RefreshLobby();
            };
            ShowConnect("");
            _status.style.color = Theme.TextDim;
            _status.text = status;
        }

        void Leave()
        {
            if (_session != null)
            {
                _session = null;
                _app.CloseNet();
                ShowConnect("");
                return;
            }
            _back();
        }

        void ShowConnect(string error)
        {
            Ui.Show(_connectView, true);
            Ui.Show(_lobbyView, false);
            _status.style.color = Theme.Danger;
            _status.text = error ?? "";
        }

        void RefreshFriends()
        {
            if (_friends == null) return;
            _friends.Clear();
            var steam = NetPlatforms.Steam;
            var lobbies = steam != null && steam.Available ? steam.FriendsInLobbies() : new System.Collections.Generic.List<JoinableLobby>();
            if (lobbies.Count == 0)
            {
                _friends.Add(Note(LocalizationService.Get("menu.online.039")));
                return;
            }
            foreach (var lobby in lobbies)
            {
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 6f;
                var name = Ui.Text(lobby.Name, 15f, Theme.Text);
                name.style.flexGrow = 1f;
                row.Add(name);
                ulong id = lobby.Lobby;
                row.Add(Ui.Button(LocalizationService.Get("menu.online.035"), () => JoinSteam(id), ButtonKind.Secondary, 14f));
                _friends.Add(row);
            }
        }

        // ================================================================== Лобби

        void RefreshLobby()
        {
            var session = _session;
            if (session == null) return;
            bool host = session.IsHost;
            if (!host && session.Seats.Count == 0) return; // ещё ждём ответа хоста
            Ui.Show(_connectView, false);
            Ui.Show(_lobbyView, true);

            _lobbyTitle.text = string.Format(LocalizationService.Get("menu.online.dynamic.lobby"), session.Transport.Kind);
            _lobbyHint.text = host ? session.Transport.ConnectHint : string.Format(LocalizationService.Get("menu.online.dynamic.host"), session.Seats[0].name);

            _seatsList.Clear();
            for (int i = 0; i < session.Seats.Count; i++)
            {
                var seat = session.Seats[i];
                var row = Ui.Row();
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 8f;
                var chip = Ui.Box();
                chip.style.width = 18f;
                chip.style.height = 18f;
                chip.style.marginRight = 12f;
                chip.style.backgroundColor = Palette.PlayerColor(i);
                Ui.Radius(chip, 4f);
                row.Add(chip);
                // Логотип клуба строителей — рядом с именем (награда за цели недели).
                if (!seat.isBot && !string.IsNullOrEmpty(seat.clubLogo))
                {
                    var logo = new ClubLogo(seat.clubLogo, 26f);
                    logo.style.marginRight = 8f;
                    logo.tooltip = string.IsNullOrEmpty(seat.clubTag) ? LocalizationService.Get("menu.online.dynamic.club") : string.Format(LocalizationService.Get("menu.online.dynamic.club_tag"), seat.clubTag);
                    logo.pickingMode = PickingMode.Position;
                    row.Add(logo);
                }
                var name = Ui.Text(seat.name + (!seat.isBot && !string.IsNullOrEmpty(seat.clubTag) ? $"  [{seat.clubTag}]" : ""), 17f, Theme.Text, true);
                name.style.flexGrow = 1f;
                row.Add(name);
                string tag = i == 0 ? LocalizationService.Get("menu.online.040") : seat.isBot ? LocalizationService.Get("menu.online.041") : LocalizationService.Get("menu.online.042");
                if (i == session.LocalSeat) tag += LocalizationService.Get("menu.online.043");
                var tagLabel = Ui.Text(tag, 14f, Theme.TextDim);
                tagLabel.style.marginRight = 12f;
                row.Add(tagLabel);
                if (host && i > 0)
                {
                    int index = i;
                    row.Add(Ui.Button(seat.isBot ? LocalizationService.Get("menu.online.044") : LocalizationService.Get("menu.online.045"), () => (_session as HostSession)?.RemoveSeat(index), ButtonKind.Ghost, 13f));
                }
                _seatsList.Add(row);
            }
            var empty = NetSession.MaxSeats - session.Seats.Count;
            if (empty > 0) _seatsList.Add(Note(string.Format(LocalizationService.Get("menu.online.dynamic.free_seats"), empty)));

            Ui.Show(_hostControls, host);
            Ui.Show(_waitLabel, !host);
            if (host)
            {
                var hostSession = (HostSession)session;
                Ui.Show(_inviteButton, session.Transport.CanInvite);
                Ui.SetEnabled(_addBotButton, hostSession.CanAddSeat);
                _timerButton.text = _turnSeconds > 0 ? string.Format(LocalizationService.Get("menu.online.dynamic.turn_timer"), _turnSeconds) : LocalizationService.Get("menu.online.046");
                _scenarioButton.text = LocalizationService.Get("menu.online.047") + ScenarioMenuLocalization.Title((ScenarioKind)Mathf.Clamp(_setup.scenario, 0, Scenarios.All.Count - 1));
                _mapButton.text = LocalizationService.Get("menu.online.048") + GameSetup.MapOf(_setup.mapResourcePath).Title;
                _modeButton.text = _setup.cooperative ? LocalizationService.Get("menu.online.049") : LocalizationService.Get("menu.online.050");
                Ui.Show(_coopDifficultyButton, _setup.cooperative);
                _coopDifficultyButton.text = CoopDifficultyText.Title(_setup.coopDifficulty);
                _coopDifficultyButton.tooltip = CoopDifficultyText.Hint(_setup.coopDifficulty);
                Ui.SetEnabled(_startButton, session.Seats.Count >= 2);
            }
        }

        readonly UnityEngine.UIElements.Button _scenarioButton;
        readonly UnityEngine.UIElements.Button _mapButton;
        readonly UnityEngine.UIElements.Button _modeButton;
        readonly UnityEngine.UIElements.Button _coopDifficultyButton;

        void CycleMap()
        {
            int index = Array.FindIndex(GameSetup.Maps, m => m.Path == GameSetup.MapOf(_setup.mapResourcePath).Path);
            _setup.mapResourcePath = GameSetup.Maps[(index + 1) % GameSetup.Maps.Length].Path;
            _setup.Save();
            RefreshLobby();
        }

        void CycleScenario()
        {
            _setup.scenario = (Mathf.Clamp(_setup.scenario, 0, Scenarios.All.Count - 1) + 1) % Scenarios.All.Count;
            _setup.Save();
            RefreshLobby();
        }

        void CycleTimer()
        {
            var options = GameSetup.TurnTimerOptions;
            int index = Array.IndexOf(options, _turnSeconds);
            _turnSeconds = options[(index + 1) % options.Length];
            RefreshLobby();
        }

        void StartGame()
        {
            if (!(_session is HostSession host) || host.Seats.Count < 2) return;
            // Те же правила раздачи персонажей и участков, что и в обычной партии.
            var setup = new GameSetup { roadPieces = _setup.roadPieces, scenario = _setup.scenario, cooperative = _setup.cooperative, coopDifficulty = _setup.coopDifficulty };
            // В сети персонажа в лобби не выбирают: каждый выберет из двух в начале партии.
            foreach (var seat in host.Seats)
                setup.players.Add(new PlayerSlot(seat.name, seat.isBot, seat.botLevel == 1 ? BotLevel.Random : BotLevel.Smart)
                    { character = seat.character == PlayerSlot.RandomCharacter ? PlayerSlot.DraftCharacter : seat.character });
            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            var characters = setup.ResolveCharacters(new System.Random(seed ^ 0x5bd1e995));
            var start = new GameStartInfo
            {
                players = setup.PlayerNames().ToArray(),
                characters = characters.Select(c => (int)c).ToArray(),
                seed = seed,
                config = setup.CreateConfig()
            };
            try
            {
                host.StartGame(start, _setup.mapResourcePath, _turnSeconds);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _status.text = LocalizationService.Get("menu.online.051") + e.Message;
            }
        }

        // ================================================================== Мелочи

        static VisualElement Title(string text, Action back)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 18f;
            var backButton = Ui.Button(LocalizationService.Get("menu.online.052"), back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            row.Add(backButton);
            row.Add(Ui.Text(text, 34f, Theme.Gold, true));
            return row;
        }

        static Label Note(string text)
        {
            var label = Ui.Text(text, 13f, Theme.TextDim);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 6f;
            return label;
        }

        static TextField Field(string value, int maxLength)
        {
            var field = new TextField { value = value, maxLength = maxLength };
            field.style.fontSize = 16f;
            field.style.color = Theme.Text;
            Ui.Margin(field, 0f);
            var input = field.Q(className: TextField.inputUssClassName) ?? field;
            input.style.backgroundColor = Theme.PanelLight;
            input.style.color = Theme.Text;
            input.style.minHeight = InterfaceLayout.OnlineInputMinHeight;
            Ui.Radius(input, 8f);
            Ui.Border(input, 1f, Theme.Line);
            Ui.Padding(input, 6f, 10f);
            return field;
        }
    }
}
