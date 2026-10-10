using Mirage;
using NoMulticrew.Networking.Messages;
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
        public required PersistentID AircraftId { get; init; }
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
            Answer(joiner, message.AircraftId, CrewJoinOutcome.AircraftLost);
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

        if (!CanSeat(aircraft, joiner, out var refusal))
        {
            Answer(joiner, aircraft.persistentID, refusal);
            return;
        }

        var request = new Request
        {
            Id = _nextId++,
            Joiner = joiner,
            Aircraft = aircraft,
            AircraftId = aircraft.persistentID,
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
            Answer(request.Joiner, request.AircraftId, CrewJoinOutcome.AircraftLost);
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
            Answer(request.Joiner, request.AircraftId, CrewJoinOutcome.Declined);
            return;
        }

        if (Violation(request.Aircraft, request.Joiner) is { } violation)
        {
            Plugin.Logger.LogWarning($"Crew request {request.Id} accepted but refused: {violation}");
            return;
        }

        if (!CanSeat(request.Aircraft, request.Joiner, out var refusal))
        {
            Plugin.Logger.LogInfo($"Crew request {request.Id} accepted but no longer valid: {refusal}");
            Answer(request.Joiner, request.AircraftId, refusal);
            return;
        }

        _session.Crew.Seat(request.Aircraft, request.Joiner);

        _session.Notify(
            request.Aircraft.Player,
            Texts.Requests.Joined(request.Joiner.GetDisplayName(PlayerNameContext.Other)),
            NoticeTone.Positive,
            CrewCue.Select
        );

        Answer(request.Joiner, request.AircraftId, CrewJoinOutcome.Seated);
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
            Answer(request.Joiner, request.AircraftId, CrewJoinOutcome.NoAnswer);
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
                Answer(request.Joiner, request.AircraftId, CrewJoinOutcome.PilotLeft);
            }
        }
    }

    private bool CanSeat(Aircraft aircraft, Player joiner, out CrewJoinOutcome refusal)
    {
        if (!SeatTable.CanBoard(aircraft, joiner, out _, out refusal))
        {
            return false;
        }

        if (!_session.TakesCrew(aircraft.Player))
        {
            refusal = CrewJoinOutcome.NotTakingCrew;
            return false;
        }

        if (_session.Crew.IsCrewed(aircraft.persistentID))
        {
            refusal = CrewJoinOutcome.SeatTaken;
            return false;
        }

        return true;
    }

    private void Answer(Player joiner, PersistentID aircraftId, CrewJoinOutcome outcome)
    {
        _session.SendToPlayer(joiner.Owner, new CrewJoinResult(aircraftId, outcome));
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