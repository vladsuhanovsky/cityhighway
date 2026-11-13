using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CityHighways.Net
{
    /// <summary>
    /// Локальная сеть (и проверка на одном компьютере): хост слушает TCP-порт, клиенты подключаются по адресу.
    /// Сообщения — кадры «длина (4 байта) + данные». Чтение идёт в фоновых потоках, а события
    /// выдаются из Poll() в главном потоке.
    /// </summary>
    public sealed class TcpTransport : INetTransport
    {
        public const int DefaultPort = 27777;
        const int MaxFrame = 4 * 1024 * 1024;
        const ulong HostPeer = 1;

        sealed class Connection
        {
            public ulong Id;
            public TcpClient Client;
            public NetworkStream Stream;
            public readonly object WriteLock = new object();
        }

        readonly ConcurrentQueue<Action> _events = new ConcurrentQueue<Action>();
        readonly Dictionary<ulong, Connection> _connections = new Dictionary<ulong, Connection>();
        readonly object _lock = new object();
        TcpListener _listener;
        volatile bool _closed;
        long _nextId = 2;

        public bool IsHost { get; }
        public ulong LocalId { get; private set; }
        public ulong HostId => HostPeer;
        public bool IsReady { get; private set; }
        public string Kind => "Локальная сеть";
        public string ConnectHint { get; private set; } = "";
        public bool CanInvite => false;
        public void Invite() { }

        public event Action Ready;
        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, byte[]> Received;
        public event Action<string> Failed;

        TcpTransport(bool isHost) { IsHost = isHost; }

        /// <summary>Открыть игру: слушать порт на всех сетевых интерфейсах.</summary>
        public static TcpTransport Host(int port = DefaultPort)
        {
            var transport = new TcpTransport(true) { LocalId = HostPeer };
            try
            {
                transport._listener = new TcpListener(IPAddress.Any, port);
                transport._listener.Start();
                var addresses = LocalAddresses();
                transport.ConnectHint = addresses.Count == 0
                    ? $"порт {port}"
                    : $"Адрес для друзей: {string.Join(" или ", addresses)} (порт {port})";
                var thread = new Thread(transport.AcceptLoop) { IsBackground = true, Name = "CityHighways TCP accept" };
                thread.Start();
                transport._events.Enqueue(() =>
                {
                    transport.IsReady = true;
                    transport.Ready?.Invoke();
                });
            }
            catch (Exception e)
            {
                transport._events.Enqueue(() => transport.Failed?.Invoke($"Не удалось открыть порт {port}: {e.Message}"));
            }
            return transport;
        }

        /// <summary>Подключиться к хосту: «192.168.1.5» или «192.168.1.5:27777».</summary>
        public static TcpTransport Connect(string address)
        {
            var transport = new TcpTransport(false);
            string host = (address ?? "").Trim();
            int port = DefaultPort;
            int colon = host.LastIndexOf(':');
            if (colon > 0 && int.TryParse(host.Substring(colon + 1), out var parsed))
            {
                port = parsed;
                host = host.Substring(0, colon);
            }
            if (host.Length == 0) host = "127.0.0.1";
            var thread = new Thread(() => transport.ConnectLoop(host, port)) { IsBackground = true, Name = "CityHighways TCP connect" };
            thread.Start();
            return transport;
        }

        public static List<string> LocalAddresses()
        {
            try
            {
                return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    .Select(a => a.ToString()).Distinct().ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        void AcceptLoop()
        {
            while (!_closed)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    return; // слушатель закрыт
                }
                client.NoDelay = true;
                var connection = new Connection
                {
                    Id = (ulong)Interlocked.Increment(ref _nextId) - 1,
                    Client = client,
                    Stream = client.GetStream()
                };
                lock (_lock) _connections[connection.Id] = connection;
                _events.Enqueue(() => PeerConnected?.Invoke(connection.Id));
                var reader = new Thread(() => ReadLoop(connection)) { IsBackground = true, Name = "CityHighways TCP read" };
                reader.Start();
            }
        }

        void ConnectLoop(string host, int port)
        {
            try
            {
                var client = new TcpClient { NoDelay = true };
                client.Connect(host, port);
                var connection = new Connection { Id = HostPeer, Client = client, Stream = client.GetStream() };
                lock (_lock) _connections[HostPeer] = connection;
                _events.Enqueue(() =>
                {
                    LocalId = 0;
                    IsReady = true;
                    Ready?.Invoke();
                    PeerConnected?.Invoke(HostPeer);
                });
                ReadLoop(connection);
            }
            catch (Exception e)
            {
                if (!_closed) _events.Enqueue(() => Failed?.Invoke($"Не удалось подключиться к {host}:{port}: {e.Message}"));
            }
        }

        void ReadLoop(Connection connection)
        {
            var header = new byte[4];
            try
            {
                while (!_closed)
                {
                    ReadExactly(connection.Stream, header, 4);
                    int length = BitConverter.ToInt32(header, 0);
                    if (length < 0 || length > MaxFrame) throw new IOException("Слишком большое сообщение.");
                    var data = new byte[length];
                    ReadExactly(connection.Stream, data, length);
                    ulong from = connection.Id;
                    _events.Enqueue(() => Received?.Invoke(from, data));
                }
            }
            catch (Exception)
            {
                // соединение закрыто
            }
            Drop(connection);
        }

        static void ReadExactly(Stream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n <= 0) throw new EndOfStreamException();
                read += n;
            }
        }

        void Drop(Connection connection)
        {
            bool removed;
            lock (_lock) removed = _connections.Remove(connection.Id);
            try { connection.Client.Close(); } catch (Exception) { }
            if (!removed || _closed) return;
            ulong id = connection.Id;
            _events.Enqueue(() =>
            {
                PeerDisconnected?.Invoke(id);
                if (!IsHost) Failed?.Invoke("Связь с хостом потеряна.");
            });
        }

        public void Send(ulong peer, byte[] data)
        {
            Connection connection;
            lock (_lock) _connections.TryGetValue(peer, out connection);
            if (connection == null) return;
            try
            {
                var frame = new byte[4 + data.Length];
                BitConverter.GetBytes(data.Length).CopyTo(frame, 0);
                data.CopyTo(frame, 4);
                lock (connection.WriteLock) connection.Stream.Write(frame, 0, frame.Length);
            }
            catch (Exception)
            {
                Drop(connection);
            }
        }

        public void Poll()
        {
            while (_events.TryDequeue(out var action)) action();
        }

        public void Dispose()
        {
            _closed = true;
            try { _listener?.Stop(); } catch (Exception) { }
            List<Connection> all;
            lock (_lock)
            {
                all = _connections.Values.ToList();
                _connections.Clear();
            }
            foreach (var c in all)
            {
                try { c.Client.Close(); } catch (Exception) { }
            }
        }
    }
}
