using NoMulticrew.Networking;
using NuclearOption.Networking;

namespace NoMulticrew.Crew;

internal sealed class CrewRegistry
{
    private readonly Dictionary<PersistentID, Player?[]> _crews = [];

    private readonly ServerSession _session;

    public CrewRegistry(ServerSession session)
    {
        _session = session;
    }

    public int? SeatOf(Player player, PersistentID aircraftId)
    {
        if (!_crews.TryGetValue(aircraftId, out var seats))
        {
            return null;
        }

        var index = Array.FindIndex(seats, x => ReferenceEquals(x, player));

        return index >= 0 ? index : null;
    }

    public bool IsCrewed(PersistentID aircraftId)
    {
        return _crews.ContainsKey(aircraftId);
    }

    public Player? OccupantOwning(Aircraft aircraft, int stationIndex)
    {
        if (!_crews.TryGetValue(aircraft.persistentID, out var seats))
        {
            return null;
        }

        for (var i = 0; i < seats.Length; i++)
        {
            if (seats[i] != null && Plugin.SeatTable.Owns(aircraft, i, stationIndex))
            {
                return seats[i];
            }
        }

        return null;
    }

    public List<Player> Occupants(PersistentID aircraftId)
    {
        return _crews.TryGetValue(aircraftId, out var seats)
            ? [.. seats.Where(x => x != null).Select(x => x!)]
            : [];
    }

    public bool IsTaken(PersistentID aircraftId, int seatIndex)
    {
        return _crews.TryGetValue(aircraftId, out var seats) && seats[seatIndex] != null;
    }

    public PersistentID? AircraftOf(Player player)
    {
        foreach (var (aircraftId, seats) in _crews)
        {
            if (seats.Any(x => ReferenceEquals(x, player)))
            {
                return aircraftId;
            }
        }

        return null;
    }

    public void Seat(Aircraft aircraft, int seatIndex, Player player)
    {
        var key = aircraft.definition.jsonKey;

        if (!_crews.TryGetValue(aircraft.persistentID, out var seats))
        {
            seats = new Player?[Plugin.SeatTable.SeatsFor(key).Count];
            _crews[aircraft.persistentID] = seats;
        }

        seats[seatIndex] = player;

        Plugin.Logger.LogInfo(
            $"{player.GetDisplayName(PlayerNameContext.Other)} took seat {seatIndex} "
            + $"({Plugin.SeatTable.Label(key, seatIndex)}) of {key} {aircraft.persistentID}"
        );

        Broadcast(aircraft.persistentID);
    }

    public void Release(Player player)
    {
        var aircraftId = AircraftOf(player);
        if (aircraftId == null)
        {
            return;
        }

        var seats = _crews[aircraftId.Value];

        UnitRegistry.TryGetUnit<Aircraft>(aircraftId.Value, out var aircraft);

        var label = "Crew";

        for (var i = 0; i < seats.Length; i++)
        {
            if (ReferenceEquals(seats[i], player))
            {
                if (aircraft != null)
                {
                    label = Plugin.SeatTable.Label(aircraft.definition.jsonKey, i);
                }

                seats[i] = null;
            }
        }

        _session.Commands.Released(player, aircraftId.Value);

        var left = $"{player.GetDisplayName(PlayerNameContext.Other)} left {label}";

        foreach (var aboard in seats.Where(x => x != null).Append(aircraft != null ? aircraft.Player : null))
        {
            if (aboard != null)
            {
                _session.Notify(aboard, left, CrewCue.Deselect);
            }
        }

        if (seats.All(x => x == null))
        {
            _crews.Remove(aircraftId.Value);
        }

        Plugin.Logger.LogInfo($"{player.GetDisplayName(PlayerNameContext.Other)} left their seat in {aircraftId.Value}");

        Broadcast(aircraftId.Value);
    }

    public void Dissolve(PersistentID aircraftId)
    {
        if (!_crews.Remove(aircraftId, out var seats))
        {
            return;
        }

        foreach (var occupant in seats.Where(x => x != null))
        {
            _session.Economy.Settle(occupant!, aircraftId, forfeit: false);
            _session.Commands.Released(occupant!, aircraftId);
        }

        Plugin.Logger.LogInfo($"Crew of {aircraftId} dissolved");

        foreach (var occupant in seats.Where(x => x != null))
        {
            _session.Notify(occupant!, "The crew was dissolved", CrewCue.Deselect);
        }

        Broadcast(aircraftId);
    }

    public void DissolvePilotedBy(Player pilot)
    {
        foreach (var aircraftId in _crews.Keys.ToList())
        {
            if (UnitRegistry.TryGetUnit(aircraftId, out var unit)
                && unit is Aircraft aircraft
                && ReferenceEquals(aircraft.Player, pilot))
            {
                Dissolve(aircraftId);
            }
        }
    }

    public void SendRoster(PersistentID aircraftId)
    {
        var seats = _crews.GetValueOrDefault(aircraftId, []);
        var occupants = seats.Select(x => x != null ? x.PlayerIndex : -1).ToArray();
        var pending = seats.Select(x => x != null ? _session.Economy.PendingOf(x) : 0f).ToArray();

        _session.SendToAllCapable(new CrewRoster(aircraftId, occupants, pending));
    }

    private void Broadcast(PersistentID aircraftId)
    {
        SendRoster(aircraftId);
        _session.Commands.Reconcile(aircraftId);

        if (UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var aircraft))
        {
            CrewState.ApplyTurrets(aircraft);
        }
    }
}