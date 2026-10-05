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
    private const float BoardingSpeed = 50f / 3.6f;

    private readonly List<Request> _requests = [];

    private readonly ServerSession _session;

    private int _nextId = 1;

    public JoinRequests(ServerSession session)
    {
        _session = session;
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

        if (!TryGetBoardingAirbase(aircraft, out _))
        {
            reason = "The aircraft is not at an airbase";
            return false;
        }

        reason = null;
        return true;
    }

    public static bool TryGetBoardingAirbase(Aircraft aircraft, [NotNullWhen(true)] out Airbase? airbase)
    {
        airbase = null;

        return aircraft.radarAlt < 5f
            && aircraft.speed < BoardingSpeed
            && aircraft.NetworkHQ != null
            && aircraft.NetworkHQ.AnyNearAirbase(aircraft.transform.position, out airbase);
    }

    public static bool IsValidExit(Aircraft aircraft)
    {
        return aircraft.speed < 2f
            && aircraft.NetworkHQ != null
            && aircraft.NetworkHQ.AnyNearAirbase(aircraft.transform.position, out _)
            && aircraft.transform.position.y > Datum.LocalSeaY;
    }

    public void OnRequest(INetworkPlayer connection, CrewJoinRequest message)
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
            ExpiresAt = Time.unscaledTime + TimeoutSeconds
        };

        var prompt = new CrewJoinPrompt(request.Id, joiner.PlayerIndex, request.SeatIndex, TimeoutSeconds);

        if (!_session.SendToPlayer(aircraft.Player.Owner, prompt))
        {
            _session.Notify(joiner, "The pilot cannot take crew");
            return;
        }

        _requests.Add(request);

        Plugin.Logger.LogInfo(
            $"{joiner.GetDisplayName(PlayerNameContext.Other)} asked for seat {request.SeatIndex} "
            + $"of {aircraft.definition.jsonKey} {aircraft.persistentID}"
        );
    }

    public void OnResponse(INetworkPlayer connection, CrewJoinResponse message)
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
            Plugin.Logger.LogInfo($"Crew request {request.Id} declined");
            _session.Notify(request.Joiner, "The pilot declined", CrewCue.Deselect);
            return;
        }

        if (!CanSeat(request.Aircraft, request.Joiner, request.SeatIndex, out var reason))
        {
            Plugin.Logger.LogInfo($"Crew request {request.Id} accepted but no longer valid: {reason}");
            _session.Notify(request.Joiner, reason, CrewCue.Deselect);
            return;
        }

        _session.Crew.Seat(request.Aircraft, request.SeatIndex, request.Joiner);

        var label = Plugin.SeatTable.Label(request.Aircraft.definition.jsonKey, request.SeatIndex);
        var joined = $"{request.Joiner.GetDisplayName(PlayerNameContext.Other)} joined as {label}";

        foreach (var aboard in _session.Crew.Occupants(request.Aircraft.persistentID).Append(request.Aircraft.Player))
        {
            if (aboard != null && !ReferenceEquals(aboard, request.Joiner))
            {
                _session.Notify(aboard, joined, CrewCue.Select);
            }
        }

        _session.Notify(request.Joiner, "Seated", CrewCue.Select);
    }

    public void Tick()
    {
        var now = Time.unscaledTime;

        for (var i = _requests.Count - 1; i >= 0; i--)
        {
            var request = _requests[i];
            if (request.ExpiresAt > now)
            {
                continue;
            }

            _requests.RemoveAt(i);
            Plugin.Logger.LogInfo($"Crew request {request.Id} timed out");
            _session.Notify(request.Joiner, "The pilot did not answer", CrewCue.Deselect);
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

        var seats = Plugin.SeatTable.SeatsFor(aircraft.definition.jsonKey);
        if (seatIndex >= seats.Count)
        {
            reason = "That aircraft has no such seat";
            return false;
        }

        if (!CrewState.OwnsAny(aircraft, seatIndex))
        {
            reason = "That seat has no weapons in this loadout";
            return false;
        }

        if (_session.Crew.IsSeated(joiner))
        {
            reason = "You need to leave your current seat first";
            return false;
        }

        if (_session.Crew.IsTaken(aircraft.persistentID, seatIndex))
        {
            reason = "That seat is taken";
            return false;
        }

        return true;
    }
}