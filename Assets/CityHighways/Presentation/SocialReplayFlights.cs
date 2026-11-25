using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityHighways.Presentation
{
    /// <summary>
    /// One HTTP replay request per record per Steam account session.
    /// Subsequent viewers of the same record share its result, but each
    /// viewer still validates the replay against its own clicked card.
    /// The token also prevents an old A -> B -> A reply from completing a
    /// new request for the same record ID.
    /// </summary>
    public sealed class SocialReplayFlights
    {
        sealed class Subscriber
        {
            public SocialCloud.RecordInfo Record;
            public Action<ReplayFile> Done;
            public Action<string> Failed;
        }

        sealed class Flight
        {
            public int Token;
            public readonly List<Subscriber> Subscribers = new List<Subscriber>();
        }

        readonly Dictionary<long, Flight> _flights = new Dictionary<long, Flight>();
        int _nextToken;

        public int Count => _flights.Count;

        static SocialCloud.RecordInfo Snapshot(SocialCloud.RecordInfo record) =>
            new SocialCloud.RecordInfo
            {
                id = record.id,
                name = record.name,
                playerId = record.playerId,
                mode = record.mode,
                mapPath = record.mapPath,
                mapName = record.mapName,
                score = record.score,
                won = record.won,
                seats = record.seats,
                date = record.date
            };

        /// <summary>
        /// Returns true only for the first subscriber (caller should issue
        /// HTTP). Returns false if joined to an already pending request.
        /// </summary>
        public bool Begin(SocialCloud.RecordInfo record,
            Action<ReplayFile> done, Action<string> failed, out int token)
        {
            token = 0;
            if (record == null || record.id <= 0) return false;
            if (_flights.TryGetValue(record.id, out var existing))
            {
                existing.Subscribers.Add(new Subscriber
                {
                    Record = Snapshot(record), Done = done, Failed = failed
                });
                token = existing.Token;
                return false;
            }

            unchecked { _nextToken++; }
            if (_nextToken == 0) _nextToken = 1;
            token = _nextToken;
            var flight = new Flight { Token = token };
            flight.Subscribers.Add(new Subscriber
            {
                Record = Snapshot(record), Done = done, Failed = failed
            });
            _flights.Add(record.id, flight);
            return true;
        }

        bool Take(long id, int token, out List<Subscriber> subscribers)
        {
            subscribers = null;
            if (!_flights.TryGetValue(id, out var flight) || flight.Token != token) return false;
            _flights.Remove(id); // Detach before callbacks may start another load.
            subscribers = flight.Subscribers;
            return true;
        }

        static void NotifySuccess(Subscriber subscriber, ReplayFile replay)
        {
            try { subscriber.Done?.Invoke(replay); }
            catch (Exception e) { Debug.LogWarning("Social replay callback failed: " + e.Message); }
        }

        static void NotifyFailure(Subscriber subscriber, string error)
        {
            try { subscriber.Failed?.Invoke(error); }
            catch (Exception e) { Debug.LogWarning("Social replay callback failed: " + e.Message); }
        }

        public bool Complete(long id, int token, ReplayFile replay, string mismatchError)
        {
            if (!Take(id, token, out var subscribers)) return false;
            foreach (var subscriber in subscribers)
            {
                if (SocialReplayMatch.Matches(subscriber.Record, replay))
                    NotifySuccess(subscriber, replay);
                else
                    NotifyFailure(subscriber, mismatchError);
            }
            return true;
        }

        public bool Fail(long id, int token, string error)
        {
            if (!Take(id, token, out var subscribers)) return false;
            foreach (var subscriber in subscribers)
                NotifyFailure(subscriber, error);
            return true;
        }

        public void CancelAll(string reason)
        {
            if (_flights.Count == 0) return;
            var pending = new List<Flight>(_flights.Values);
            _flights.Clear();
            foreach (var flight in pending)
                foreach (var subscriber in flight.Subscribers)
                    NotifyFailure(subscriber, reason);
        }
    }
}
