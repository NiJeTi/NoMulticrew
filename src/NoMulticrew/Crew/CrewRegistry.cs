using Mirage;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;

namespace NoMulticrew.Crew;

internal sealed class CrewRegistry
{
    private readonly Dictionary<PersistentID, Player> _wsos = [];
    private readonly Dictionary<PersistentID, byte> _wsoStations = [];
    private readonly Dictionary<PersistentID, byte> _pilotStations = [];

    private readonly ServerSession _session;

    public CrewRegistry(ServerSession session)
    {
        _session = session;
    }

    public Player? WsoOf(PersistentID aircraftId)
    {
        return _wsos.GetValueOrDefault(aircraftId);
    }

    public bool IsCrewed(PersistentID aircraftId)
    {
        return _wsos.ContainsKey(aircraftId);
    }

    public SeatState ServerState(Aircraft aircraft)
    {
        var id = aircraft.persistentID;
        var aboard = _wsos.ContainsKey(id);

        return new SeatState(
            aboard,
            aboard ? SeatTable.StationIndex(_wsoStations.GetValueOrDefault(id, SeatTable.NoStation)) : -1,
            SeatTable.StationIndex(_pilotStations.GetValueOrDefault(id, SeatTable.NoStation))
        );
    }

    public Player? WsoHolding(Aircraft aircraft, int station)
    {
        return Plugin.SeatTable.Holder(aircraft, station, ServerState(aircraft)) == Role.Wso
            ? _wsos[aircraft.persistentID]
            : null;
    }

    public PersistentID? AircraftOf(Player player)
    {
        foreach (var (aircraftId, wso) in _wsos)
        {
            if (ReferenceEquals(wso, player))
            {
                return aircraftId;
            }
        }

        return null;
    }

    public void Seat(Aircraft aircraft, Player player)
    {
        _wsos[aircraft.persistentID] = player;

        _pilotStations.TryAdd(
            aircraft.persistentID,
            aircraft.weaponManager.currentWeaponStation is { } current ? current.Number : SeatTable.NoStation
        );

        Plugin.Logger.LogInfo(
            $"{player.GetDisplayName(PlayerNameContext.Other)} took the WSO seat of "
            + $"{aircraft.definition.jsonKey} {aircraft.persistentID}"
        );

        Broadcast(aircraft.persistentID);
    }

    public void Select(PersistentID aircraftId, byte station)
    {
        _wsoStations[aircraftId] = station;

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

        if (!IsCrewed(aircraft.persistentID))
        {
            return;
        }

        _wsoStations.Remove(aircraft.persistentID);

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

        _wsos.Remove(aircraftId.Value);
        _wsoStations.Remove(aircraftId.Value);

        _session.Commands.Released(player, aircraftId.Value);

        UnitRegistry.TryGetUnit<Aircraft>(aircraftId.Value, out var aircraft);

        if (aircraft != null && aircraft.Player != null)
        {
            _session.Notify(
                aircraft.Player,
                $"{player.GetDisplayName(PlayerNameContext.Other)} left {SeatTable.Label(Role.Wso)}",
                CrewCue.Deselect
            );
        }

        Plugin.Logger.LogInfo($"{player.GetDisplayName(PlayerNameContext.Other)} left their seat in {aircraftId.Value}");

        Broadcast(aircraftId.Value);
    }

    public void Dissolve(PersistentID aircraftId)
    {
        if (!_wsos.Remove(aircraftId, out var wso))
        {
            return;
        }

        _wsoStations.Remove(aircraftId);
        _session.Economy.Settle(wso, aircraftId, forfeit: false);
        _session.Commands.Released(wso, aircraftId);

        Plugin.Logger.LogInfo($"Crew of {aircraftId} dissolved");

        _session.Notify(wso, "The crew was dissolved", CrewCue.Deselect);

        Broadcast(aircraftId);
    }

    public void DissolvePilotedBy(Player pilot)
    {
        foreach (var aircraftId in _wsos.Keys.ToList())
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
        foreach (var aircraftId in _wsos.Keys.ToList())
        {
            _session.SendToPlayer(player, Roster(aircraftId));
        }
    }

    private CrewRoster Roster(PersistentID aircraftId)
    {
        var wso = WsoOf(aircraftId);

        return new CrewRoster(
            aircraftId,
            wso != null ? wso.PlayerIndex : -1,
            wso != null ? _session.Economy.PendingOf(wso) : 0f,
            _wsoStations.GetValueOrDefault(aircraftId, SeatTable.NoStation),
            _pilotStations.GetValueOrDefault(aircraftId, SeatTable.NoStation)
        );
    }

    private void Broadcast(PersistentID aircraftId)
    {
        SendRoster(aircraftId);
        _session.Commands.Reconcile(aircraftId);

        if (UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var aircraft))
        {
            Plugin.SeatTable.ApplyTurrets(aircraft, ServerState(aircraft));
        }
    }
}
