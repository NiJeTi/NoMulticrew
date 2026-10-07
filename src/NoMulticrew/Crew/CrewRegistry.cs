using Mirage;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;

namespace NoMulticrew.Crew;

internal sealed class CrewRegistry
{
    private readonly Dictionary<PersistentID, Player?[]> _crews = [];
    private readonly Dictionary<Player, byte> _stations = [];
    private readonly Dictionary<PersistentID, byte> _pilotStations = [];

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

    public SeatState StateOf(Aircraft aircraft)
    {
        var wso = _crews.TryGetValue(aircraft.persistentID, out var seats) ? seats[SeatTable.Wso] : null;
        var pilot = _pilotStations.GetValueOrDefault(aircraft.persistentID, SeatTable.NoStation);

        return new SeatState(
            wso != null,
            wso != null ? SeatTable.StationIndex(_stations.GetValueOrDefault(wso, SeatTable.NoStation)) : -1,
            SeatTable.StationIndex(pilot)
        );
    }

    public Player? Holder(Aircraft aircraft, int stationIndex)
    {
        return Plugin.SeatTable.Holder(aircraft, stationIndex, StateOf(aircraft)) == SeatTable.Wso
            ? _crews[aircraft.persistentID][SeatTable.Wso]
            : null;
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

        _pilotStations.TryAdd(
            aircraft.persistentID,
            aircraft.weaponManager.currentWeaponStation is { } current ? current.Number : SeatTable.NoStation
        );

        Plugin.Logger.LogInfo(
            $"{player.GetDisplayName(PlayerNameContext.Other)} took seat {seatIndex} "
            + $"({SeatTable.Label(seatIndex)}) of {key} {aircraft.persistentID}"
        );

        Broadcast(aircraft.persistentID);
    }

    public void Select(Player player, PersistentID aircraftId, byte station)
    {
        _stations[player] = station;

        Broadcast(aircraftId);
    }

    public void RecordPilotStation(Aircraft aircraft, byte station)
    {
        _pilotStations[aircraft.persistentID] = station;

        if (IsCrewed(aircraft.persistentID)
            && (Plugin.SeatTable.IsShared(aircraft) || Plugin.SeatTable.PanelOf(aircraft) != null))
        {
            SendRoster(aircraft.persistentID);
        }
    }

    public void LoadoutChanged(Aircraft aircraft)
    {
        _pilotStations[aircraft.persistentID] = aircraft.weaponStations.Count > 0 ? (byte)0 : SeatTable.NoStation;

        if (!_crews.TryGetValue(aircraft.persistentID, out var seats))
        {
            return;
        }

        foreach (var occupant in seats.Where(x => x != null))
        {
            _stations.Remove(occupant!);
        }

        Broadcast(aircraft.persistentID);
    }

    public void Release(Player player, bool forfeit)
    {
        var aircraftId = AircraftOf(player);
        if (aircraftId == null)
        {
            return;
        }

        _session.Economy.Settle(player, aircraftId.Value, forfeit);

        var seats = _crews[aircraftId.Value];

        UnitRegistry.TryGetUnit<Aircraft>(aircraftId.Value, out var aircraft);

        var label = "Crew";

        for (var i = 0; i < seats.Length; i++)
        {
            if (ReferenceEquals(seats[i], player))
            {
                label = SeatTable.Label(i);

                _stations.Remove(player);
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
            _stations.Remove(occupant!);
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
        _session.SendToAllCapable(Roster(aircraftId));
    }

    public void SendRosters(INetworkPlayer player)
    {
        foreach (var aircraftId in _crews.Keys.ToList())
        {
            _session.SendToPlayer(player, Roster(aircraftId));
        }
    }

    private CrewRoster Roster(PersistentID aircraftId)
    {
        var seats = _crews.GetValueOrDefault(aircraftId, []);
        var occupants = seats.Select(x => x != null ? x.PlayerIndex : -1).ToArray();
        var pending = seats.Select(x => x != null ? _session.Economy.PendingOf(x) : 0f).ToArray();

        var selected = seats
            .Select(x => x != null ? _stations.GetValueOrDefault(x, SeatTable.NoStation) : SeatTable.NoStation)
            .ToArray();
        var pilot = _pilotStations.GetValueOrDefault(aircraftId, SeatTable.NoStation);

        return new CrewRoster(aircraftId, occupants, pending, selected, pilot);
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