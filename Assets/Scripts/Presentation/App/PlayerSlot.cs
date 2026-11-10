using System;
using RailGame.Core;
using UnityEngine;

namespace RailGame.Presentation.App
{
    /// <summary>Участник партии из главного меню: имя, человек или бот, уровень бота.</summary>
    [Serializable]
    public class PlayerSlot
    {
        public string name = "Игрок";
        public bool isBot;
        [Tooltip("Smart — строит пути под задания; Random — случайные ходы.")]
        public BotLevel botLevel = BotLevel.Smart;

        public PlayerSlot() { }

        public PlayerSlot(string name, bool isBot, BotLevel botLevel = BotLevel.Smart)
        {
            this.name = name;
            this.isBot = isBot;
            this.botLevel = botLevel;
        }
    }
}
