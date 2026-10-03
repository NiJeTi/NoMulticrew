using System.Diagnostics.CodeAnalysis;
using Mirage;
using NoMulticrew.Networking;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Crew;

internal sealed class JoinRequests
{
    private sealed class Request
    {
        public required int Id { get; init; }
        public required Player Joiner { get; init; }
        public required Aircraft Aircraft { get; init; }
        public required byte SeatIndex { get; init; }
        public required float ExpiresAt { get; init; }
    }

    private const float TimeoutSeconds = 10f;

    private readonly List<Request> _requests = [];

    private readonly ServerSession _session;
    private readonly CrewRegistry _crew;

    private int _nextId = 1;

    public JoinRequests(ServerSession session, CrewRegistry crew)
    {
        _session = session;
        _crew = crew;
    }

    public static bool CanBoard(Aircraft aircraft, Player joiner, [NotNullWhen(false)] out string? reason)
    {
        if (aircraft.disabled || aircraft.Player == null)
        {
            reason = "That aircraft has no pilot";
            return false;
        }

        if (aircraft.NetworkHQ == null || joiner.HQ != aircraft.NetworkHQ)
        {
            reason = "That aircraft belongs to the opposing faction";
            return false;
        }

        if (joiner.Aircraft != null)
        {
            reason = "You need to leave your aircraft first";
            return false;
        }

        if (!TryGetAirbase(aircraft, out _))
        {
            reason = "The aircraft is not at an airbase";
            return false;
        }

        reason = null;
        return true;
    }

    public static bool TryGetAirbase(Aircraft aircraft, [NotNullWhen(true)] out Airbase? airbase)
    {
        airbase = null;

        return aircraft.IsLanded()
               && aircraft.NetworkHQ != null
               && aircraft.NetworkHQ.AnyNearAirbase(aircraft.transform.position, out airbase);
    }

    public void OnRequest(INetworkPlayer connection, MulticrewJoinRequest message)
    {
        if (!_session.TryGetPlayer(connection, out var joiner))
        {
            return;
        }

        if (!UnitRegistry.TryGetUnit(message.AircraftId, out var unit) || unit is not Aircraft aircraft)
        {
            _session.Notify(joiner, "That aircraft no longer exists");
            return;
        }

        if (_requests.Any(x => ReferenceEquals(x.Joiner, joiner)))
        {
            _session.Notify(joiner, "You already have a request outstanding");
            return;
        }

        if (!CanSeat(aircraft, joiner, message.SeatIndex, out var reason))
        {
            _session.Notify(joiner, reason);
            return;
        }

        var request = new Request
        {
            Id = _nextId++,
            Joiner = joiner,
            Aircraft = aircraft,
            SeatIndex = message.SeatIndex,
            ExpiresAt = Time.timeSinceLevelLoad + TimeoutSeconds
        };

        var prompt = new MulticrewJoinPrompt(request.Id, joiner.PlayerIndex, request.SeatIndex, TimeoutSeconds);

        if (!_session.SendToPlayer(aircraft.Player.Owner, prompt))
        {
            _session.Notify(joiner, "The pilot cannot take crew");
            return;
        }

        _requests.Add(request);
    }

    public void OnResponse(INetworkPlayer connection, MulticrewJoinResponse message)
    {
        if (!_session.TryGetPlayer(connection, out var responder))
        {
            return;
        }

        var request = _requests.FirstOrDefault(x => x.Id == message.RequestId);
        if (request == null)
        {
            return;
        }

        if (request.Aircraft == null)
        {
            _requests.Remove(request);
            _session.Notify(request.Joiner, "That aircraft no longer exists");
            return;
        }

        if (!ReferenceEquals(request.Aircraft.Player, responder))
        {
            Plugin.Logger.LogWarning($"Crew join response {message.RequestId} came from someone other than the pilot");
            return;
        }

        _requests.Remove(request);

        if (!message.Accepted)
        {
            _session.Notify(request.Joiner, "The pilot declined");
            return;
        }

        if (!CanSeat(request.Aircraft, request.Joiner, request.SeatIndex, out var reason))
        {
            _session.Notify(request.Joiner, reason);
            return;
        }

        _crew.Seat(request.Aircraft, request.SeatIndex, request.Joiner);
        _session.Notify(request.Joiner, "Seated");
    }

    public void Tick()
    {
        var now = Time.timeSinceLevelLoad;

        for (var i = _requests.Count - 1; i >= 0; i--)
        {
            var request = _requests[i];
            if (request.ExpiresAt > now)
            {
                continue;
            }

            _requests.RemoveAt(i);
            _session.Notify(request.Joiner, "The pilot did not answer");
        }
    }

    public void Forget(Player player)
    {
        _requests.RemoveAll(x => ReferenceEquals(x.Joiner, player));
    }

    public void Clear()
    {
        _requests.Clear();
    }

    private bool CanSeat(Aircraft aircraft, Player joiner, byte seatIndex, [NotNullWhen(false)] out string? reason)
    {
        if (!CanBoard(aircraft, joiner, out reason))
        {
            return false;
        }

        if (seatIndex >= Plugin.SeatTable.SeatsFor(aircraft.definition.jsonKey).Count)
        {
            reason = "That aircraft has no such seat";
            return false;
        }

        if (_crew.IsSeated(joiner))
        {
            reason = "You need to leave your current seat first";
            return false;
        }

        if (_crew.IsTaken(aircraft.persistentID, seatIndex))
        {
            reason = "That seat is taken";
            return false;
        }

        return true;
    }
}
