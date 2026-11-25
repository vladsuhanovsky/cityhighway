using System;
using CityHighways.Core;
using CityHighways.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public class SaveReplayCompatibilityAuditTests
    {
        [TestCase("moscow")]
        [TestCase("paris")]
        [TestCase("berlin")]
        public void CurrentSaveAndReplayRoundTrip(string city)
        {
            var map = CityHighwaysApp.LoadMap("CityHighways/Maps/" + city);
            var game = new Game(map, new[] { "A", "B" }, new GameConfig(), 42);
            var data = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(game.CreateSave()));
            Assert.AreEqual(GameSaveData.CurrentVersion, data.version);
            Assert.IsTrue(game.SameGameState(Game.Load(map, data)));
            Assert.IsTrue(game.SameGameState(Game.Replay(map, data.start, data.history)));
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(GameSaveData.CurrentVersion + 1)]
        public void LoadRejectsUnsupportedVersionBeforeReplay(int version)
        {
            var map = CityHighwaysApp.LoadMap("CityHighways/Maps/moscow");
            var data = new Game(map, new[] { "A", "B" }, new GameConfig(), 42).CreateSave();
            data.version = version;
            Assert.Throws<FormatException>(() => Game.Load(map, data));
        }
    }
}
