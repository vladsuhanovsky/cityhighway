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
            _connectView.Add(Title("Сетевая игра", Leave));

            var nameRow = Ui.Row();
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 14f;
            nameRow.Add(Ui.Text("Ваше имя", 16f, Theme.TextDim));
            _nameField = Field(DefaultName(), 16);
            _nameField.style.width = 320f;
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
            ugsCard.Add(Ui.Text("Через интернет", 22f, Theme.Accent, true));
            if (NetPlatforms.Ugs == null)
            {
                ugsCard.Add(Note("Нужен пакет Multiplayer Services и привязка проекта к Unity Cloud — как настроить, написано в README, раздел «Unity Relay и Lobby»."));
            }
            else
            {
                ugsCard.Add(Note("Серверы Unity (Relay): играть можно из разных сетей, без настройки роутера."));
                var hostUgs = Ui.Button("Создать лобби", HostUgs, ButtonKind.Primary, 17f);
                hostUgs.style.marginTop = 10f;
                ugsCard.Add(hostUgs);
                var codeRow = Ui.Row();
                codeRow.style.alignItems = Align.Center;
                codeRow.style.marginTop = 14f;
                _codeField = Field("", 12);
                _codeField.style.flexGrow = 1f;
                codeRow.Add(_codeField);
                var byCode = Ui.Button("Войти по коду", () => JoinUgs(_codeField.value, true), ButtonKind.Secondary, 15f);
                byCode.style.marginLeft = 10f;
                codeRow.Add(byCode);
                ugsCard.Add(codeRow);
                var listHeader = Ui.Row();
                listHeader.style.alignItems = Align.Center;
                listHeader.style.marginTop = 16f;
                var listTitle = Ui.Text("Открытые игры", 15f, Theme.Text, true);
                listTitle.style.flexGrow = 1f;
                listHeader.Add(listTitle);
                listHeader.Add(Ui.Button("Обновить", RefreshOpenLobbies, ButtonKind.Ghost, 13f));
                ugsCard.Add(listHeader);
                _openLobbies = Ui.Column();
                _openLobbies.style.marginTop = 6f;
                _openLobbies.Add(Note("Нажмите «Обновить», чтобы найти игры."));
                ugsCard.Add(_openLobbies);
            }
            columns.Add(ugsCard);

            // Steam
            var steamCard = Ui.Card(18f);
            steamCard.style.flexGrow = 1f;
            steamCard.style.flexBasis = 0f;
            steamCard.style.marginRight = 14f;
            steamCard.Add(Ui.Text("Steam", 22f, Theme.Accent, true));
            var steam = NetPlatforms.Steam;
            if (steam == null)
            {
                steamCard.Add(Note("Steamworks.NET не установлен в проект, поэтому Steam недоступен. Как подключить — в README, раздел «Сетевая игра». А пока можно играть по локальной сети."));
            }
            else if (!steam.Available)
            {
                steamCard.Add(Note(steam.Problem));
            }
            else
            {
                steamCard.Add(Note("Лобби только для друзей. Друзья подключаются по приглашению из оверлея Steam или из списка ниже."));
                var host = Ui.Button("Создать лобби в Steam", HostSteam, ButtonKind.Primary, 17f);
                host.style.marginTop = 10f;
                steamCard.Add(host);
                var friendsHeader = Ui.Row();
                friendsHeader.style.alignItems = Align.Center;
                friendsHeader.style.marginTop = 16f;
                var friendsTitle = Ui.Text("Друзья в лобби City Highways", 15f, Theme.Text, true);
                friendsTitle.style.flexGrow = 1f;
                friendsHeader.Add(friendsTitle);
                friendsHeader.Add(Ui.Button("Обновить", RefreshFriends, ButtonKind.Ghost, 13f));
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
            lanCard.Add(Ui.Text("Локальная сеть", 22f, Theme.Accent, true));
            lanCard.Add(Note($"Компьютеры в одной сети (или два окна игры на одном компьютере). Хост открывает порт {TcpTransport.DefaultPort}; брандмауэр может спросить разрешение."));
            var hostLan = Ui.Button("Создать игру", HostLan, ButtonKind.Primary, 17f);
            hostLan.style.marginTop = 10f;
            lanCard.Add(hostLan);
            var joinRow = Ui.Row();
            joinRow.style.alignItems = Align.Center;
            joinRow.style.marginTop = 16f;
            _addressField = Field(PlayerPrefs.GetString(AddressKey, "127.0.0.1"), 64);
            _addressField.style.flexGrow = 1f;
            joinRow.Add(_addressField);
            var join = Ui.Button("Подключиться", JoinLan, ButtonKind.Secondary, 15f);
            join.style.marginLeft = 10f;
            joinRow.Add(join);
            lanCard.Add(joinRow);
            lanCard.Add(Note("Адрес хоста, например 192.168.1.5 (порт можно указать через двоеточие)."));
            columns.Add(lanCard);

            _status = Ui.Text("", 15f, Theme.Danger);
            _status.style.marginTop = 14f;
            _status.style.whiteSpace = WhiteSpace.Normal;
            _connectView.Add(_status);

            // ------------------------------------------------------------ Лобби
            _lobbyView = Ui.Column();
            Root.Add(_lobbyView);
            var lobbyHeader = Title("Лобби", Leave);
            _lobbyTitle = lobbyHeader.Q<Label>();
            _lobbyView.Add(lobbyHeader);
            _lobbyHint = Note("");
            _lobbyHint.style.marginBottom = 10f;
            _lobbyView.Add(_lobbyHint);

            var seatsCard = Ui.Card(18f);
            seatsCard.Add(Ui.Text("За столом", 18f, Theme.Text, true));
            _seatsList = Ui.Column();
            _seatsList.style.marginTop = 10f;
            seatsCard.Add(_seatsList);
            _lobbyView.Add(seatsCard);

            _hostControls = Ui.Row();
            _hostControls.style.marginTop = 14f;
            _hostControls.style.flexWrap = Wrap.Wrap;
            _inviteButton = Ui.Button("Пригласить друга", () => _session?.Transport.Invite(), ButtonKind.Secondary, 15f);
            _addBotButton = Ui.Button("Добавить бота", () => (_session as HostSession)?.AddBot(0), ButtonKind.Secondary, 15f);
            _timerButton = Ui.Button("", CycleTimer, ButtonKind.Secondary, 15f);
            _startButton = Ui.Button("Начать партию", StartGame, ButtonKind.Primary, 17f);
            foreach (var b in new[] { _inviteButton, _addBotButton, _timerButton })
            {
                b.style.marginRight = 10f;
                _hostControls.Add(b);
            }
            var spacer = Ui.Box();
            spacer.style.flexGrow = 1f;
            _hostControls.Add(spacer);
            _hostControls.Add(_startButton);
            _lobbyView.Add(_hostControls);

            _waitLabel = Ui.Text("Ждём, пока хост начнёт партию…", 16f, Theme.TextDim);
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
            var first = _setup.players.FirstOrDefault(p => !p.isBot);
            return first != null ? first.name : "Игрок";
        }

        string PlayerName => string.IsNullOrWhiteSpace(_nameField.value) ? "Игрок" : _nameField.value.Trim();

        // ================================================================== Подключение

        void HostLan() => Open(new HostSession(TcpTransport.Host(), CityHighwaysApp.LoadMap, PlayerName), "Создаём игру…");

        void JoinLan()
        {
            PlayerPrefs.SetString(AddressKey, _addressField.value);
            Open(new ClientSession(TcpTransport.Connect(_addressField.value), CityHighwaysApp.LoadMap, PlayerName), "Подключаемся…");
        }

        void HostUgs()
        {
            var transport = NetPlatforms.Ugs?.CreateHost($"{PlayerName} · City Highways", NetSession.MaxSeats);
            if (transport == null)
            {
                ShowConnect("Unity Relay недоступен.");
                return;
            }
            Open(new HostSession(transport, CityHighwaysApp.LoadMap, PlayerName), "Создаём лобби на серверах Unity…");
        }

        void JoinUgs(string codeOrId, bool byCode)
        {
            if (byCode && string.IsNullOrWhiteSpace(codeOrId))
            {
                ShowConnect("Введите код лобби — его видит хост в своём лобби.");
                return;
            }
            var transport = byCode ? NetPlatforms.Ugs?.JoinByCode(codeOrId) : NetPlatforms.Ugs?.JoinById(codeOrId);
            if (transport == null)
            {
                ShowConnect("Unity Relay недоступен.");
                return;
            }
            Open(new ClientSession(transport, CityHighwaysApp.LoadMap, PlayerName), "Входим в лобби…");
        }

        void RefreshOpenLobbies()
        {
            if (_openLobbies == null || NetPlatforms.Ugs == null) return;
            _openLobbies.Clear();
            _openLobbies.Add(Note("Ищем…"));
            NetPlatforms.Ugs.QueryLobbies((lobbies, error) =>
            {
                _openLobbies.Clear();
                if (error != null)
                {
                    _openLobbies.Add(Note("Не удалось получить список: " + error));
                    return;
                }
                if (lobbies.Count == 0)
                {
                    _openLobbies.Add(Note("Открытых игр нет. Создайте свою или войдите по коду."));
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
                    row.Add(Ui.Button("Войти", () => JoinUgs(id, false), ButtonKind.Secondary, 14f));
                    _openLobbies.Add(row);
                }
            });
        }

        void HostSteam()
        {
            var transport = NetPlatforms.Steam?.CreateHost(NetSession.MaxSeats);
            if (transport == null)
            {
                ShowConnect("Steam недоступен.");
                return;
            }
            Open(new HostSession(transport, CityHighwaysApp.LoadMap, PlayerName), "Создаём лобби в Steam…");
        }

        /// <summary>Подключиться к лобби друга (из списка, по приглашению или с +connect_lobby).</summary>
        public void JoinSteam(ulong lobby)
        {
            var transport = NetPlatforms.Steam?.Join(lobby);
            if (transport == null)
            {
                ShowConnect("Steam недоступен.");
                return;
            }
            Open(new ClientSession(transport, CityHighwaysApp.LoadMap, PlayerName), "Входим в лобби друга…");
        }

        void Open(NetSession session, string status)
        {
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
                _friends.Add(Note("Пока никого. Попросите друга создать лобби и нажмите «Обновить» — или примите его приглашение в Steam."));
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
                row.Add(Ui.Button("Войти", () => JoinSteam(id), ButtonKind.Secondary, 14f));
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

            _lobbyTitle.text = $"Лобби · {session.Transport.Kind}";
            _lobbyHint.text = host ? session.Transport.ConnectHint : $"Хост: {session.Seats[0].name}. Он добавляет ботов и начинает партию.";

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
                var name = Ui.Text(seat.name, 17f, Theme.Text, true);
                name.style.flexGrow = 1f;
                row.Add(name);
                string tag = i == 0 ? "хост" : seat.isBot ? "бот" : "игрок";
                if (i == session.LocalSeat) tag += " · вы";
                var tagLabel = Ui.Text(tag, 14f, Theme.TextDim);
                tagLabel.style.marginRight = 12f;
                row.Add(tagLabel);
                if (host && i > 0)
                {
                    int index = i;
                    row.Add(Ui.Button(seat.isBot ? "Убрать" : "Отключить", () => (_session as HostSession)?.RemoveSeat(index), ButtonKind.Ghost, 13f));
                }
                _seatsList.Add(row);
            }
            var empty = NetSession.MaxSeats - session.Seats.Count;
            if (empty > 0) _seatsList.Add(Note($"Свободных мест: {empty}. Персонажи раздаются случайно при старте."));

            Ui.Show(_hostControls, host);
            Ui.Show(_waitLabel, !host);
            if (host)
            {
                var hostSession = (HostSession)session;
                Ui.Show(_inviteButton, session.Transport.CanInvite);
                Ui.SetEnabled(_addBotButton, hostSession.CanAddSeat);
                _timerButton.text = _turnSeconds > 0 ? $"Таймер хода: {_turnSeconds} с" : "Таймер хода: нет";
                Ui.SetEnabled(_startButton, session.Seats.Count >= 2);
            }
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
            var setup = new GameSetup { roadPieces = _setup.roadPieces };
            foreach (var seat in host.Seats)
                setup.players.Add(new PlayerSlot(seat.name, seat.isBot, seat.botLevel == 1 ? BotLevel.Random : BotLevel.Smart) { character = seat.character });
            int seed = UnityEngine.Random.Range(0, int.MaxValue);
            var characters = setup.ResolveCharacters(new System.Random(seed ^ 0x5bd1e995));
            var start = new GameStartInfo
            {
                players = setup.PlayerNames().ToArray(),
                characters = characters.Select(c => (int)c).ToArray(),
                seed = seed,
                config = new GameConfig { RoadPiecesOverride = _setup.roadPieces }
            };
            try
            {
                host.StartGame(start, _setup.mapResourcePath, _turnSeconds);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _status.text = "Не удалось начать: " + e.Message;
            }
        }

        // ================================================================== Мелочи

        static VisualElement Title(string text, Action back)
        {
            var row = Ui.Row();
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 18f;
            var backButton = Ui.Button("← Назад", back, ButtonKind.Ghost, 15f);
            backButton.style.marginRight = 16f;
            row.Add(backButton);
            row.Add(Ui.Text(text, 34f, Theme.Accent, true));
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
            input.style.minHeight = 38f;
            Ui.Radius(input, 8f);
            Ui.Border(input, 1f, Theme.Line);
            Ui.Padding(input, 6f, 10f);
            return field;
        }
    }
}
