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
            var client = new ClientSession(hub.Connect(), LoadSmall, "Клиент");
            Pump(new NetSession[] { host, client });
            host.StartGame(Start(host, 9), "small", 0);
            Pump(new NetSession[] { host, client });

            client.Transport.Dispose(); // связь оборвалась без «до свидания»
            Pump(new NetSession[] { host });
            Assert.IsTrue(host.Seats[1].isBot);
            Assert.IsFalse(host.Seats[1].connected);

            // Хост доигрывает пару ходов, затем игрок возвращается под тем же именем.
            var bots = new IBot[] { new SmartBot(1), new SmartBot(2) };
            for (int i = 0; i < 6; i++)
                Assert.IsTrue(host.Perform(g => bots[g.ActingPlayerIndex].Act(g)).Success);
            Pump(new NetSession[] { host });

            var back = new ClientSession(hub.Connect(), LoadSmall, "Клиент");
            Pump(new NetSession[] { host, back });
            Assert.IsTrue(back.InGame);
            Assert.AreEqual(1, back.LocalSeat);
            Assert.IsFalse(host.Seats[1].isBot, "Место снова у человека");
            Assert.AreEqual(Json(host.Game), Json(back.Game));
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
