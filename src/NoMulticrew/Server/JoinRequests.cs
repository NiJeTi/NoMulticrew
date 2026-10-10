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
            _session.Notify(joiner, Texts.Requests.AircraftGone);
            return;
        }

        if (Violation(aircraft, joiner) is { } violation)
        {
            Plugin.Logger.LogWarning(
                $"Refused crew request of {joiner.GetDisplayName(PlayerNameContext.Other)} "
                + $"for {aircraft.persistentID}: {violation}"
            );
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

        _session.SendToPlayer(aircraft.Player.Owner, prompt);

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
            _session.Notify(request.Joiner, Texts.Requests.AircraftGone);
            return;
        }

        if (!ReferenceEquals(request.Aircraft.Player, responder))
        {
            Plugin.Logger.LogWarning($"Crew join response {message.RequestId} ignored: responder is not the pilot");
            return;
        }

        _requests.Remove(request);

        if (!message.Accepted)
        {
            Plugin.Logger.LogInfo($"Crew request {request.Id} declined");
            _session.Notify(request.Joiner, Texts.Requests.PilotDeclined, CrewCue.Deselect);
            return;
        }

        if (Violation(request.Aircraft, request.Joiner) is { } violation)
        {
            Plugin.Logger.LogWarning($"Crew request {request.Id} accepted but refused: {violation}");
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
            Texts.Requests.Joined(request.Joiner.GetDisplayName(PlayerNameContext.Other)),
            CrewCue.Select
        );

        _session.Notify(request.Joiner, Texts.Requests.Seated, CrewCue.Select);
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
            _session.Notify(request.Joiner, Texts.Requests.PilotSilent, CrewCue.Deselect);
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
                _session.Notify(request.Joiner, Texts.Requests.PilotLeft, CrewCue.Deselect);
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
            reason = Texts.Requests.NotTakingCrew;
            return false;
        }

        if (_session.Crew.IsCrewed(aircraft.persistentID))
        {
            reason = Texts.Requests.SeatTaken;
            return false;
        }

        return true;
    }

    private string? Violation(Aircraft aircraft, Player joiner)
    {
        if (_requests.Any(x => ReferenceEquals(x.Joiner, joiner)))
        {
            return "request already pending";
        }

        if (aircraft.NetworkHQ == null || joiner.HQ != aircraft.NetworkHQ)
        {
            return "opposing faction";
        }

        if (Plugin.SeatTable.WsoSeat(aircraft) == null)
        {
            return "aircraft has no WSO seat";
        }

        if (!Plugin.SeatTable.Offered(aircraft))
        {
            return "loadout has no WSO weapons";
        }

        if (_session.Crew.AircraftOf(joiner) != null)
        {
            return "joiner already seated";
        }

        return null;
    }
}