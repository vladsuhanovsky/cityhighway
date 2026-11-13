using System;
using System.Collections.Generic;

namespace CityHighways.Net
{
    /// <summary>
    /// «Сеть» в памяти: хост и клиенты в одном процессе. Для тестов синхронизации — сообщения доходят
    /// только при Poll() получателя, как и в настоящей сети (с задержкой на кадр).
    /// </summary>
    public sealed class LoopbackHub
    {
        internal readonly Dictionary<ulong, LoopbackTransport> Nodes = new Dictionary<ulong, LoopbackTransport>();
        ulong _nextId = 1;
        public LoopbackTransport Host { get; private set; }

        public LoopbackTransport CreateHost()
        {
            Host = new LoopbackTransport(this, _nextId++, true, 0);
            Nodes[Host.LocalId] = Host;
            Host.Enqueue(() => Host.RaiseReady());
            return Host;
        }

        public LoopbackTransport Connect()
        {
            if (Host == null) throw new InvalidOperationException("Сначала создайте хост.");
            var client = new LoopbackTransport(this, _nextId++, false, Host.LocalId);
            Nodes[client.LocalId] = client;
            client.Enqueue(() =>
            {
                client.RaiseReady();
                client.RaisePeerConnected(Host.LocalId);
            });
            Host.Enqueue(() => Host.RaisePeerConnected(client.LocalId));
            return client;
        }

        internal void Remove(LoopbackTransport node)
        {
            if (!Nodes.Remove(node.LocalId)) return;
            if (node.IsHost)
            {
                foreach (var other in Nodes.Values)
                {
                    var o = other;
                    o.Enqueue(() => o.RaisePeerDisconnected(node.LocalId));
                }
            }
            else if (Host != null && Nodes.ContainsKey(Host.LocalId))
            {
                Host.Enqueue(() => Host.RaisePeerDisconnected(node.LocalId));
            }
        }
    }

    public sealed class LoopbackTransport : INetTransport
    {
        readonly LoopbackHub _hub;
        readonly Queue<Action> _inbox = new Queue<Action>();

        internal LoopbackTransport(LoopbackHub hub, ulong id, bool isHost, ulong hostId)
        {
            _hub = hub;
            LocalId = id;
            IsHost = isHost;
            HostId = isHost ? id : hostId;
        }

        public bool IsHost { get; }
        public ulong LocalId { get; }
        public ulong HostId { get; }
        public bool IsReady { get; private set; }
        public string Kind => "Петля";
        public string ConnectHint => "";
        public bool CanInvite => false;
        public void Invite() { }
        public void SetJoinable(bool joinable) { }

        public event Action Ready;
        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, byte[]> Received;
        public event Action<string> Failed;

        internal void Enqueue(Action action) => _inbox.Enqueue(action);
        internal void RaiseReady() { IsReady = true; Ready?.Invoke(); }
        internal void RaisePeerConnected(ulong peer) => PeerConnected?.Invoke(peer);
        internal void RaisePeerDisconnected(ulong peer) => PeerDisconnected?.Invoke(peer);

        public void Send(ulong peer, byte[] data)
        {
            if (!_hub.Nodes.TryGetValue(peer, out var target)) return;
            var copy = (byte[])data.Clone();
            ulong from = LocalId;
            target.Enqueue(() => target.Received?.Invoke(from, copy));
        }

        public void Poll()
        {
            int count = _inbox.Count; // новые сообщения, пришедшие во время обработки, — в следующий Poll
            for (int i = 0; i < count && _inbox.Count > 0; i++) _inbox.Dequeue()();
        }

        public void Dispose()
        {
            _hub.Remove(this);
            Failed = null;
        }
    }
}
