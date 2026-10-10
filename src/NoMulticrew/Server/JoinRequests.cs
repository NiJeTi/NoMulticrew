using System.Diagnostics.CodeAnalysis;
using Mirage;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Server;

internal sealed class JoinRequests
{
    private sealed class Request
    {
        public required int Id { get; init; }
        public required Player Joiner { get; init; }
        public required Aircraft Aircraft { get; init; }
        public required float ExpiresAt { get; init; }
    }

    public const float TimeoutSeconds = 10f;

    private readonly List<Request> _requests = [];

    private readonly ServerSession _session;

    private int _nextId = 1;

    public JoinRequests(ServerSession session)
    {
        _session = session;
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

        if (!CanSeat(aircraft, joiner, out var reason))
        {
            _session.Notify(joiner, reason);
            return;
        }

        var request = new Request
        {
            Id = _nextId++,
            Joiner = joiner,
            Aircraft = aircraft,
            ExpiresAt = Time.unscaledTime + TimeoutSeconds
        };

        var prompt = new CrewJoinPrompt(request.Id, joiner.PlayerIndex, TimeoutSeconds);

        if (!_session.SendToPlayer(aircraft.Player.Owner, prompt))
        {
            _session.Notify(joiner, "The pilot cannot take crew");
            return;
        }

        _requests.Add(request);

        Plugin.Logger.LogInfo(
            $"{joiner.GetDisplayName(PlayerNameContext.Other)} asked for the WSO seat "
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

        if (!CanSeat(request.Aircraft, request.Joiner, out var reason))
        {
            Plugin.Logger.LogInfo($"Crew request {request.Id} accepted but no longer valid: {reason}");
            _session.Notify(request.Joiner, reason, CrewCue.Deselect);
            return;
        }

        _session.Crew.Seat(request.Aircraft, request.Joiner);

        _session.Notify(
            request.Aircraft.Player,
            $"{request.Joiner.GetDisplayName(PlayerNameContext.Other)} joined as {SeatTable.Label(Role.Wso)}",
            CrewCue.Select
        );

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
        for (var i = _requests.Count - 1; i >= 0; i--)
        {
            var request = _requests[i];
            var toPilot = request.Aircraft != null && ReferenceEquals(request.Aircraft.Player, player);

            if (!toPilot && !ReferenceEquals(request.Joiner, player))
            {
                continue;
            }

            _requests.RemoveAt(i);
            Plugin.Logger.LogInfo(
                $"Crew request {request.Id} dropped: {player.GetDisplayName(PlayerNameContext.Other)} disconnected"
            );

            if (toPilot)
            {
                _session.Notify(request.Joiner, "The pilot left", CrewCue.Deselect);
            }
        }
    }

    private bool CanSeat(Aircraft aircraft, Player joiner, [NotNullWhen(false)] out string? reason)
    {
        if (!SeatTable.CanBoard(aircraft, joiner, out _, out reason))
        {
            return false;
        }

        if (!_session.TakesCrew(aircraft.Player))
        {
            reason = "The pilot is not taking crew";
            return false;
        }

        if (Plugin.SeatTable.WsoSeat(aircraft) == null)
        {
            reason = "That aircraft has no such seat";
            return false;
        }

        if (!Plugin.SeatTable.Offered(aircraft))
        {
            reason = "That seat has no weapons in this loadout";
            return false;
        }

        if (_session.Crew.AircraftOf(joiner) != null)
        {
            reason = "You need to leave your current seat first";
            return false;
        }

        if (_session.Crew.IsCrewed(aircraft.persistentID))
        {
            reason = "That seat is taken";
            return false;
        }

        return true;
    }
}