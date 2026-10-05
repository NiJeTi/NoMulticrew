using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;

namespace NoMulticrew.Crew;

internal sealed class CrewRegistry
{
    private sealed class CrewSeat(SeatRole role)
    {
        public SeatRole Role { get; } = role;

        public Player? Occupant { get; set; }
    }

    private readonly Dictionary<PersistentID, CrewSeat[]> _crews = [];

    private readonly ServerSession _session;

    public CrewRegistry(ServerSession session)
    {
        _session = session;
    }

    public SeatRole? RoleOf(Player player, PersistentID aircraftId)
    {
        return _crews.TryGetValue(aircraftId, out var seats)
            ? seats.FirstOrDefault(x => ReferenceEquals(x.Occupant, player))?.Role
            : null;
    }

    public bool IsSeated(Player player)
    {
        return _crews.Values.Any(ss => ss.Any(s => ReferenceEquals(s.Occupant, player)));
    }

    public bool HasGunner(PersistentID aircraftId)
    {
        return _crews.TryGetValue(aircraftId, out var seats)
            && seats.Any(x => x.Occupant != null && x.Role == SeatRole.Gunner);
    }

    public bool IsCrewed(PersistentID aircraftId)
    {
        return _crews.ContainsKey(aircraftId);
    }

    public Player? OccupantOwning(Aircraft aircraft, int stationIndex)
    {
        return _crews.TryGetValue(aircraft.persistentID, out var seats)
            ? seats.FirstOrDefault(x => x.Occupant != null && CrewState.Owns(x.Role, aircraft, stationIndex))?.Occupant
            : null;
    }

    public List<Player> Occupants(PersistentID aircraftId)
    {
        return _crews.TryGetValue(aircraftId, out var seats)
            ? [.. seats.Where(x => x.Occupant != null).Select(x => x.Occupant!)]
            : [];
    }

    public bool IsTaken(PersistentID aircraftId, int seatIndex)
    {
        return _crews.TryGetValue(aircraftId, out var seats) && seats[seatIndex].Occupant != null;
    }

    public PersistentID? AircraftOf(Player player)
    {
        foreach (var (aircraftId, seats) in _crews)
        {
            if (seats.Any(x => ReferenceEquals(x.Occupant, player)))
            {
                return aircraftId;
            }
        }

        return null;
    }

    public void Seat(Aircraft aircraft, int seatIndex, Player player)
    {
        if (!_crews.TryGetValue(aircraft.persistentID, out var seats))
        {
            seats = [.. Plugin.SeatTable.SeatsFor(aircraft.definition.jsonKey).Select(x => new CrewSeat(x.Role))];
            _crews[aircraft.persistentID] = seats;
        }

        seats[seatIndex].Occupant = player;

        Plugin.Logger.LogInfo(
            $"{player.GetDisplayName(PlayerNameContext.Other)} took seat {seatIndex} ({seats[seatIndex].Role}) "
            + $"of {aircraft.definition.jsonKey} {aircraft.persistentID}"
        );

        Broadcast(aircraft.persistentID);
    }

    public bool Release(Player player)
    {
        var aircraftId = AircraftOf(player);
        if (aircraftId == null)
        {
            return false;
        }

        var seats = _crews[aircraftId.Value];

        foreach (var seat in seats.Where(x => ReferenceEquals(x.Occupant, player)))
        {
            seat.Occupant = null;
        }

        if (seats.All(x => x.Occupant == null))
        {
            _crews.Remove(aircraftId.Value);
        }

        Plugin.Logger.LogInfo($"{player.GetDisplayName(PlayerNameContext.Other)} left their seat in {aircraftId.Value}");

        Broadcast(aircraftId.Value);

        return true;
    }

    public void Dissolve(PersistentID aircraftId)
    {
        if (!_crews.Remove(aircraftId, out var seats))
        {
            return;
        }

        Plugin.Logger.LogInfo($"Crew of {aircraftId} dissolved");

        foreach (var seat in seats.Where(seat => seat.Occupant != null))
        {
            _session.Notify(seat.Occupant!, "The crew was dissolved");
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

    public void Clear()
    {
        _crews.Clear();
    }

    private void Broadcast(PersistentID aircraftId)
    {
        var seats = _crews.GetValueOrDefault(aircraftId, []);

        var occupants = seats.Select(x => x.Occupant != null ? x.Occupant.PlayerIndex : -1).ToArray();
        var roles = seats.Select(x => x.Role).ToArray();

        _session.SendToAllCapable(new CrewRoster(aircraftId, occupants, roles));

        if (UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var aircraft))
        {
            CrewState.ApplyTurrets(aircraft);
        }
    }
}