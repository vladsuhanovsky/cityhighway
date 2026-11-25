using System;
using System.Collections.Generic;
using System.Linq;
using CityHighways.Core;
using CityHighways.Net;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    /// <summary>
    /// Сетевая партия через «сеть в памяти»: лобби, старт, ходы хоста, клиента и бота, отключение игрока.
    /// Главное условие — у хоста и клиентов после каждой рассылки одна и та же партия.
    /// </summary>
    public class NetSessionTests
    {
        static CityMap LoadSmall(string path) => TestCities.Small();

        static void Pump(IEnumerable<NetSession> sessions, int rounds = 3)
        {
            for (int i = 0; i < rounds; i++)
                foreach (var s in sessions) s.Poll();
        }

        static string Json(Game game) => JsonUtility.ToJson(game.CreateSave());

        static GameStartInfo Start(HostSession host, int seed) => new GameStartInfo
        {
            players = host.Seats.Select(s => s.name).ToArray(),
            characters = new int[host.Seats.Count],
            seed = seed,
            config = new GameConfig()
        };

        [Test]
        public void Lobby_JoinAddBotAndStart_GivesEveryoneTheSameGame()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Хост");
            var client = new ClientSession(hub.Connect(), LoadSmall, "Клиент");
            var all = new NetSession[] { host, client };
            Pump(all);

            Assert.AreEqual(2, host.Seats.Count);
            Assert.AreEqual(2, client.Seats.Count, "Клиент получил состав лобби");
            Assert.AreEqual(1, client.LocalSeat);

            host.AddBot(0);
            Pump(all);
            Assert.AreEqual(3, client.Seats.Count);
            Assert.IsTrue(client.Seats[2].isBot);

            host.StartGame(Start(host, 11), "small", 90);
            Pump(all);
            Assert.IsTrue(client.InGame);
            Assert.AreEqual(90, client.TurnSeconds);
            Assert.AreEqual(Json(host.Game), Json(client.Game));
        }

        /// <summary>Вся партия: место 0 — хост, место 1 — клиент, место 2 — бот хоста. Состояния совпадают на каждом шаге.</summary>
        [Test]
        public void FullGame_HostClientAndBot_StayInSync()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Хост");
            var client = new ClientSession(hub.Connect(), LoadSmall, "Клиент");
            var all = new NetSession[] { host, client };
            Pump(all);
            host.AddBot(0);
            Pump(all);
            host.StartGame(Start(host, 5), "small", 0);
            Pump(all);

            var bots = Enumerable.Range(0, 3).Select(i => (IBot)new SmartBot(100 + i)).ToArray();
            int steps = 0;
            while (host.Game.Phase != GamePhase.GameOver)
            {
                Assert.Less(++steps, 20000, "Партия не закончилась");
                int acting = host.Game.ActingPlayerIndex;
                if (acting == client.LocalSeat)
                {
                    // Клиент ходит только по своей (уже догнавшей хоста) партии.
                    if (!client.AwaitingHost && client.Game.History.Count == host.Game.History.Count)
                    {
                        var result = client.Perform(g => bots[acting].Act(g));
                        Assert.IsTrue(result.Success, result.Error);
                    }
                }
                else
                {
                    var result = host.Perform(g => bots[acting].Act(g));
                    Assert.IsTrue(result.Success, result.Error);
                }
                Pump(all);
                if (client.Game.History.Count == host.Game.History.Count && steps % 25 == 0)
                    Assert.AreEqual(Json(host.Game), Json(client.Game), $"Расхождение на шаге {steps}");
            }
            Pump(all, 5);
            Assert.AreEqual(host.Game.History.Count, client.Game.History.Count);
            Assert.AreEqual(Json(host.Game), Json(client.Game));
            Assert.AreEqual(GamePhase.GameOver, client.Game.Phase);
        }

        [Test]
        public void ClientMove_OutOfTurn_IsNotSent_AndHostRejectsForeignCommands()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Хост");
            var client = new ClientSession(hub.Connect(), LoadSmall, "Клиент");
            var all = new NetSession[] { host, client };
            Pump(all);
            host.StartGame(Start(host, 3), "small", 0);
            Pump(all);

            // Стартовые контракты: сначала выбирает хост (место 0). Клиент сейчас не может ходить.
            Assert.AreEqual(0, host.Game.ActingPlayerIndex);
            var copyAttempt = client.Perform(g => g.DrawCardFromDeck());
            Assert.IsFalse(copyAttempt.Success);
            Assert.IsFalse(client.AwaitingHost, "Неудачное действие хосту не отправляется");

            // Подделка: клиент шлёт команду за хоста — хост не принимает, партия не меняется.
            int before = host.Game.History.Count;
            var forged = new NetMessage
            {
                type = NetMessage.Submit,
                from = before,
                commands = new[] { GameCommand.KeepInitialContracts(0, host.Game.Players[0].PendingContracts.Take(2).Select(c => c.Id)) }
            };
            client.Transport.Send(client.Transport.HostId, NetCodec.Encode(forged));
            string notice = null;
            client.Notice += text => notice = text;
            Pump(all);
            Assert.AreEqual(before, host.Game.History.Count);
            Assert.IsNotNull(notice, "Клиент узнал, что ход отклонён");
        }

        [Test]
        public void ClientDisconnect_InGame_SeatBecomesBot_AndCanReturn()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Хост");
            string key = ReconnectCredentialCache.NewKey();
            var client = new ClientSession(hub.Connect(), LoadSmall, "Клиент", reconnectKey: key);
            Pump(new NetSession[] { host, client });
            host.StartGame(Start(host, 9), "small", 0);
            Pump(new NetSession[] { host, client });

            client.Transport.Dispose(); // связь оборвалась без «до свидания»
            Pump(new NetSession[] { host });
            Assert.IsTrue(host.Seats[1].isBot);
            Assert.IsFalse(host.Seats[1].connected);

            // Хост доигрывает пару ходов, затем игрок возвращается с ключом места.
            var bots = new IBot[] { new SmartBot(1), new SmartBot(2) };
            for (int i = 0; i < 6; i++)
                Assert.IsTrue(host.Perform(g => bots[g.ActingPlayerIndex].Act(g)).Success);
            Pump(new NetSession[] { host });

            var back = new ClientSession(hub.Connect(), LoadSmall, "Новое имя", reconnectKey: key);
            Pump(new NetSession[] { host, back });
            Assert.IsTrue(back.InGame);
            Assert.AreEqual(1, back.LocalSeat);
            Assert.IsFalse(host.Seats[1].isBot, "Место снова у человека");
            Assert.AreEqual(Json(host.Game), Json(back.Game));
        }

        [Test]
        public void SameNicknameCannotTakeDisconnectedSeat()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            string key = ReconnectCredentialCache.NewKey();
            var original = new ClientSession(hub.Connect(), LoadSmall, "Client", reconnectKey: key);
            Pump(new NetSession[] { host, original });
            host.StartGame(Start(host, 9), "small", 0);
            Pump(new NetSession[] { host, original });
            original.Transport.Dispose();
            Pump(new NetSession[] { host });
            var stranger = new ClientSession(hub.Connect(), LoadSmall, "Client");
            Pump(new NetSession[] { host, stranger });
            Assert.IsTrue(stranger.IsClosed);
            Assert.IsFalse(stranger.InGame);
            Assert.IsTrue(host.Seats[1].isBot);
            var back = new ClientSession(hub.Connect(), LoadSmall, "Client", reconnectKey: key);
            Pump(new NetSession[] { host, back });
            Assert.IsTrue(back.InGame);
            Assert.AreEqual(1, back.LocalSeat);
            Assert.IsFalse(host.Seats[1].isBot);
        }

        [Test]
        public void ReconnectKeyCannotClaimTwoLiveSeats()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            string key = ReconnectCredentialCache.NewKey();
            var first = new ClientSession(hub.Connect(), LoadSmall, "First", reconnectKey: key);
            Pump(new NetSession[] { host, first });
            var duplicate = new ClientSession(hub.Connect(), LoadSmall, "Second", reconnectKey: key);
            Pump(new NetSession[] { host, first, duplicate });
            Assert.IsTrue(duplicate.IsClosed);
            Assert.AreEqual(2, host.Seats.Count);
            Assert.IsFalse(first.IsClosed);
            host.StartGame(Start(host, 9), "small", 0);
            Pump(new NetSession[] { host, first });
            var activeClaim = new ClientSession(hub.Connect(), LoadSmall, "Second", reconnectKey: key);
            Pump(new NetSession[] { host, first, activeClaim });
            Assert.IsTrue(activeClaim.IsClosed);
            Assert.IsTrue(first.InGame);
            Assert.IsTrue(host.Seats[1].connected);
        }

        [Test]
        public void DuplicateNamesReconnectToTheirOwnSeatsAndKeysAreNotBroadcast()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            string firstKey = ReconnectCredentialCache.NewKey(), secondKey = ReconnectCredentialCache.NewKey();
            var first = new ClientSession(hub.Connect(), LoadSmall, "Same", reconnectKey: firstKey);
            var second = new ClientSession(hub.Connect(), LoadSmall, "Same", reconnectKey: secondKey);
            var all = new NetSession[] { host, first, second };
            var observed = new List<string>();
            first.Transport.Received += (_, data) => observed.Add(System.Text.Encoding.UTF8.GetString(data));
            Pump(all);
            Assert.AreNotEqual(host.Seats[1].name, host.Seats[2].name);
            host.StartGame(Start(host, 9), "small", 0);
            Pump(all);
            second.Transport.Dispose();
            Pump(new NetSession[] { host, first });
            var back = new ClientSession(hub.Connect(), LoadSmall, "Same", reconnectKey: secondKey);
            Pump(new NetSession[] { host, first, back });
            Assert.AreEqual(2, back.LocalSeat);
            Assert.IsTrue(first.InGame);
            Assert.AreEqual(Json(host.Game), Json(back.Game));
            Assert.IsNotEmpty(observed);
            Assert.IsFalse(observed.Any(x => x.Contains(firstKey) || x.Contains(secondKey)));
        }

        [Test]
        public void KickedLobbySeatDoesNotReserveCredential()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            string key = ReconnectCredentialCache.NewKey();
            var client = new ClientSession(hub.Connect(), LoadSmall, "Client", reconnectKey: key);
            Pump(new NetSession[] { host, client });
            host.RemoveSeat(1);
            Pump(new NetSession[] { host, client });
            Assert.IsTrue(client.IsClosed);
            var fresh = new ClientSession(hub.Connect(), LoadSmall, "Client", reconnectKey: key);
            Pump(new NetSession[] { host, fresh });
            Assert.IsTrue(fresh.Joined);
            Assert.AreEqual(2, host.Seats.Count);
        }

        [Test]
        public void InvalidHelloAndOldProtocolAreRejectedWithoutCreatingSeat()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            var raw = hub.Connect();
            string rejection = null;
            raw.Received += (_, data) => { var packet = NetCodec.Decode(data); if (packet.type == NetMessage.Reject) rejection = packet.text; };
            raw.Send(raw.HostId, NetCodec.Encode(new NetMessage { type = NetMessage.Hello, version = NetCodec.ProtocolVersion, name = "Client" }));
            host.Poll();
            raw.Poll();
            Assert.IsNotNull(rejection);
            Assert.AreEqual(1, host.Seats.Count);
            rejection = null;
            raw.Send(raw.HostId, NetCodec.Encode(new NetMessage { type = NetMessage.Hello, version = NetCodec.ProtocolVersion - 1, reconnectKey = ReconnectCredentialCache.NewKey() }));
            host.Poll();
            raw.Poll();
            Assert.IsNotNull(rejection);
            Assert.AreEqual(1, host.Seats.Count);
        }

        [Test]
        public void ClientWaitsForFullSyncAndRejectsDuplicateOrGapPackets()
        {
            double now = 0;
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            var client = new ClientSession(hub.Connect(), LoadSmall, "Client", () => now);
            var all = new NetSession[] { host, client };
            Pump(all);
            host.StartGame(Start(host, 9), "small", 0);
            Pump(all);
            Assert.IsTrue(host.Perform(g => g.KeepInitialContracts(0, g.Players[0].PendingContracts.Take(2).Select(c => c.Id).ToList())).Success);
            Pump(all);
            Assert.IsTrue(client.Perform(g => g.KeepInitialContracts(1, g.Players[1].PendingContracts.Take(2).Select(c => c.Id).ToList())).Success);
            Assert.IsTrue(client.AwaitingHost);
            host.Transport.Send(client.Transport.LocalId, NetCodec.Encode(new NetMessage
            {
                type = NetMessage.Commands, from = 0, commands = host.Game.History.ToArray()
            }));
            client.Poll();
            Assert.IsTrue(client.AwaitingHost, "Duplicate batch is not acknowledgement");
            now = ClientRecovery.TimeoutSeconds;
            client.Poll();
            Assert.IsTrue(client.Synchronizing);
            Assert.IsTrue(client.AwaitingHost);
            Assert.IsFalse(client.Perform(g => g.Pass()).Success);
            host.Transport.Send(client.Transport.LocalId, NetCodec.Encode(new NetMessage
            {
                type = NetMessage.Commands, from = 99, commands = new[] { GameCommand.Pass(0) }
            }));
            client.Poll();
            Assert.IsTrue(client.Synchronizing);
            Pump(all, 5);
            Assert.IsFalse(client.AwaitingHost);
            Assert.AreEqual(Json(host.Game), Json(client.Game));
        }

        [Test]
        public void RecoveryClosesAfterBoundedSyncAttemptsAndIgnoresLatePackets()
        {
            double now = 0;
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            var client = new ClientSession(hub.Connect(), LoadSmall, "Client", () => now);
            var all = new NetSession[] { host, client };
            Pump(all);
            host.StartGame(Start(host, 9), "small", 0);
            Pump(all);
            int closed = 0;
            client.Closed += _ => closed++;
            host.Transport.Send(client.Transport.LocalId, NetCodec.Encode(new NetMessage
            {
                type = NetMessage.Commands, from = 99, commands = new[] { GameCommand.Pass(0) }
            }));
            client.Poll();
            Assert.IsTrue(client.Synchronizing);
            now = 6;
            client.Poll();
            Assert.IsFalse(client.IsClosed);
            now = 12;
            client.Poll();
            Assert.IsTrue(client.IsClosed);
            Assert.AreEqual(1, closed);
            Assert.IsFalse(client.Perform(g => g.Pass()).Success);
            Pump(all);
            Assert.AreEqual(1, closed);
        }

        [Test]
        public void InvalidStartClosesWithoutThrowingOrStartingGame()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            var client = new ClientSession(hub.Connect(), LoadSmall, "Client");
            Pump(new NetSession[] { host, client });
            int started = 0, closed = 0;
            client.Started += () => started++;
            client.Closed += _ => closed++;
            host.Transport.Send(client.Transport.LocalId, NetCodec.Encode(new NetMessage { type = NetMessage.Start }));
            Assert.DoesNotThrow(() => client.Poll());
            Assert.IsTrue(client.IsClosed);
            Assert.IsFalse(client.InGame);
            Assert.AreEqual(0, started);
            Assert.AreEqual(1, closed);
        }

        [Test]
        public void StaleSyncDoesNotRollBackAcceptedHistory()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            var client = new ClientSession(hub.Connect(), LoadSmall, "Client");
            var all = new NetSession[] { host, client };
            Pump(all);
            host.StartGame(Start(host, 9), "small", 0);
            Pump(all);
            Assert.IsTrue(host.Perform(g => g.KeepInitialContracts(0, g.Players[0].PendingContracts.Take(2).Select(c => c.Id).ToList())).Success);
            Pump(all);
            string before = Json(client.Game);
            int resynced = 0;
            client.Resynced += () => resynced++;
            host.Transport.Send(client.Transport.LocalId, NetCodec.Encode(new NetMessage
            {
                type = NetMessage.Sync, start = host.Game.StartInfo, commands = new GameCommand[0], seats = host.Seats.ToArray()
            }));
            client.Poll();
            Assert.AreEqual(before, Json(client.Game));
            Assert.AreEqual(0, resynced);
        }

        [Test]
        public void ClosedHostCannotPerformOrChangeLobby()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Host");
            host.Dispose();
            host.AddBot(0);
            Assert.AreEqual(1, host.Seats.Count);
            Assert.IsFalse(host.Perform(g => g.Pass()).Success);
        }

        [Test]
        public void HostLeaves_ClientIsClosedWithMessage()
        {
            var hub = new LoopbackHub();
            var host = new HostSession(hub.CreateHost(), LoadSmall, "Хост");
            var client = new ClientSession(hub.Connect(), LoadSmall, "Клиент");
            Pump(new NetSession[] { host, client });
            string closed = null;
            client.Closed += text => closed = text;
            host.Dispose();
            Pump(new NetSession[] { client });
            Assert.IsNotNull(closed);
            Assert.IsTrue(client.IsClosed);
        }
    }
}
