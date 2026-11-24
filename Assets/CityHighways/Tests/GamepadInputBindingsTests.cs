using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CityHighways.Tests
{
    public sealed class GamepadInputBindingsTests
    {
        [Serializable]
        sealed class InputBinding
        {
            public string action;
            public string path;
        }

        [Serializable]
        sealed class InputMap
        {
            public string name;
            public InputBinding[] bindings;
        }

        [Serializable]
        sealed class InputAsset
        {
            public InputMap[] maps;
        }

        [Test]
        public void RightStickIsNotAlsoBoundToUiNavigate()
        {
            string path = Path.Combine(Application.dataPath, "Settings",
                "InputSystem_Actions.inputactions");
            var input = JsonUtility.FromJson<InputAsset>(File.ReadAllText(path));
            Assert.IsNotNull(input);
            var ui = input.maps.First(m => m.name == "UI");
            Assert.IsFalse(ui.bindings.Any(b => b.action == "Navigate" &&
                b.path.StartsWith("<Gamepad>/rightStick/", StringComparison.Ordinal)));
            Assert.IsTrue(ui.bindings.Any(b => b.action == "Navigate" &&
                b.path == "<Gamepad>/dpad"));
            Assert.IsTrue(ui.bindings.Any(b => b.action == "Navigate" &&
                b.path == "<Gamepad>/leftStick/up"));
        }

        [Test]
        public void GameplayLookBindingStillUsesRightStick()
        {
            string path = Path.Combine(Application.dataPath, "Settings",
                "InputSystem_Actions.inputactions");
            var input = JsonUtility.FromJson<InputAsset>(File.ReadAllText(path));
            var player = input.maps.First(m => m.name == "Player");
            Assert.IsTrue(player.bindings.Any(b => b.action == "Look" &&
                b.path == "<Gamepad>/rightStick"));
        }
    }
}
