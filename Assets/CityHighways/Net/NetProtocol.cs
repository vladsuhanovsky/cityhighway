using System;
using System.Text;
using CityHighways.Core;
using UnityEngine;

namespace CityHighways.Net
{
    /// <summary>Место за столом сетевой партии.</summary>
    [Serializable]
    public class NetSeat
    {
        public string name = "Игрок";
        public bool isBot;
        /// <summary>0 — умный бот, 1 — случайный (как BotLevel в меню).</summary>
        public int botLevel;
        /// <summary>Персонаж: -1 — случайный, иначе CharacterKind как int.</summary>
        public int character = -1;
        /// <summary>Сетевой id участника (строкой: JsonUtility плохо дружит с ulong). Пусто — хост или бот.</summary>
        public string peer = "";
        public bool connected = true;
        /// <summary>Оформление из профиля игрока (эмблема, титул, рамка) — только для вида.</summary>
        public string emblem = "";
        public string title = "";
        public string frame = "";
        /// <summary>Наряд персонажа (строка «слот=вещь;…»).</summary>
        public string outfit = "";

        public ulong PeerId => ulong.TryParse(peer, out var id) ? id : 0UL;
    }

    /// <summary>
    /// Одно сообщение протокола. Плоский класс для JsonUtility; заполняются только нужные поля.
    /// Хост — единственный источник правды: клиенты присылают ему команды (submit), он проверяет их на своей
    /// партии и рассылает всем принятые команды по порядку (cmds). Партия детерминирована, поэтому одинаковые
    /// начальные условия и одинаковые команды дают у всех одинаковое состояние.
    /// </summary>
    [Serializable]
    public class NetMessage
    {
        public const string Hello = "hello";        // клиент → хост: имя и версия протокола
        public const string Lobby = "lobby";        // хост → все: кто за столом
        public const string Reject = "reject";      // хост → клиент: отказ (текст)
        public const string Start = "start";        // хост → клиент: начало партии (или переподключение)
        public const string Submit = "submit";      // клиент → хост: команды своего хода
        public const string Commands = "cmds";      // хост → все: принятые команды начиная с from
        public const string SyncRequest = "sync_req"; // клиент → хост: пришлите партию целиком
        public const string Sync = "sync";          // хост → клиент: начальные условия и вся история
        public const string Seats = "seats";        // хост → все: места изменились во время партии
        public const string Bye = "bye";            // кто угодно: ухожу / вас отключили

        public string type;
        public int version;
        public string name;
        public int seat = -1;
        public int from;
        public GameCommand[] commands;
        public GameStartInfo start;
        public NetSeat[] seats;
        public string map;
        public int turnSeconds;
        public string text;
        /// <summary>hello: оформление профиля подключающегося.</summary>
        public string emblem;
        public string title;
        public string frame;
        public string outfit;
    }

    public static class NetCodec
    {
        /// <summary>Меняется при любой несовместимой правке протокола или правил: разные версии не играют вместе.</summary>
        public const int ProtocolVersion = 7; // 2 — влияние, 3 — аукцион мэрии, 4 — взятки, 5 — профили, 6 — персонаж, 7 — пробки и пропуска

        public static byte[] Encode(NetMessage message) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));

        public static GameStartInfo RoundTrip(GameStartInfo start) =>
            JsonUtility.FromJson<GameStartInfo>(JsonUtility.ToJson(start));

        public static NetMessage Decode(byte[] data)
        {
            try
            {
                return JsonUtility.FromJson<NetMessage>(Encoding.UTF8.GetString(data));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
