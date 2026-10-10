using Mirage;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;

namespace NoMulticrew.Server;

internal sealed class CrewRegistry
{
    private sealed class Entry
    {
        public Player? Wso { get; set; }
        public int WsoStation { get; set; } = -1;
        public int PilotStation { get; set; } = -1;
    }

    private readonly Dictionary<PersistentID, Entry> _entries = [];

    private readonly ServerSession _session;

    public CrewRegistry(ServerSession session)
    {
        _session = session;
    }

    public int Count => _entries.Count;

    public Player? WsoOf(PersistentID aircraftId)
    {
        return _entries.GetValueOrDefault(aircraftId)?.Wso;
    }

    public bool IsCrewed(PersistentID aircraftId)
    {
        return WsoOf(aircraftId) != null;
    }

    public SeatState ServerState(Aircraft aircraft)
    {
        if (!_entries.TryGetValue(aircraft.persistentID, out var entry))
        {
            return new SeatState(false, -1, -1);
        }

        var aboard = entry.Wso != null;

        return new SeatState(
            aboard,
            aboard ? entry.WsoStation : -1,
            entry.PilotStation
        );
    }

    public Player? WsoHolding(Aircraft aircraft, int station)
    {
        return Plugin.SeatTable.Holder(aircraft, station, ServerState(aircraft)) == Role.Wso
            ? WsoOf(aircraft.persistentID)
            : null;
    }

    public PersistentID? AircraftOf(Player player)
    {
        foreach (var (aircraftId, entry) in _entries)
        {
            if (ReferenceEquals(entry.Wso, player))
            {
                return aircraftId;
            }
        }

        return null;
    }

    public void Seat(Aircraft aircraft, Player player)
    {
        var entry = EntryOf(aircraft.persistentID);

        entry.Wso = player;

        if (entry.PilotStation < 0)
        {
            entry.PilotStation = aircraft.weaponManager.currentWeaponStation?.Number ?? -1;
        }

        Plugin.Logger.LogInfo(
            $"{player.GetDisplayName(PlayerNameContext.Other)} took the WSO seat of "
            + $"{aircraft.definition.jsonKey} {aircraft.persistentID}"
        );

        Broadcast(aircraft.persistentID);
    }

    public void Select(PersistentID aircraftId, int station)
    {
        EntryOf(aircraftId).WsoStation = station;

        Broadcast(aircraftId);
    }

    public void RecordPilotStation(Aircraft aircraft, byte station)
    {
        EntryOf(aircraft.persistentID).PilotStation = station;

        if (IsCrewed(aircraft.persistentID)
            && (Plugin.SeatTable.IsShared(aircraft) || Plugin.SeatTable.PanelOf(aircraft) != null))
        {
            SendRoster(aircraft.persistentID);
        }
    }

    public void LoadoutChanged(Aircraft aircraft)
    {
        var entry = EntryOf(aircraft.persistentID);

        entry.PilotStation = aircraft.weaponStations.Count > 0 ? 0 : -1;

        if (entry.Wso == null)
        {
            return;
        }

        entry.WsoStation = -1;

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

        var entry = _entries[aircraftId.Value];
        entry.Wso = null;
        entry.WsoStation = -1;

        _session.Commands.Released(player, aircraftId.Value);

        UnitRegistry.TryGetUnit<Aircraft>(aircraftId.Value, out var aircraft);

        if (aircraft != null && aircraft.Player != null)
        {
            _session.Notify(
                aircraft.Player,
                Texts.Crew.CrewmateLeft(player.GetDisplayName(PlayerNameContext.Other)),
                CrewCue.Deselect
            );
        }

        Plugin.Logger.LogInfo(
            $"{player.GetDisplayName(PlayerNameContext.Other)} left their seat in {aircraftId.Value}"
        );

        Broadcast(aircraftId.Value);
    }

    public void Dissolve(PersistentID aircraftId)
    {
        if (!_entries.Remove(aircraftId, out var entry))
        {
            return;
        }

        var wso = entry.Wso;
        if (ReferenceEquals(wso, null))
        {
            return;
        }

        _session.Economy.Settle(wso, aircraftId, forfeit: false);
        _session.Commands.Released(wso, aircraftId);

        Plugin.Logger.LogInfo($"Crew of {aircraftId} dissolved");

        Broadcast(aircraftId);
    }

    public void DissolvePilotedBy(Player pilot)
    {
        foreach (var aircraftId in _entries.Keys.ToList())
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
        foreach (var (aircraftId, entry) in _entries.ToList())
        {
            if (entry.Wso != null)
            {
                _session.SendToPlayer(player, Roster(aircraftId));
            }
        }
    }

    private Entry EntryOf(PersistentID aircraftId)
    {
        if (!_entries.TryGetValue(aircraftId, out var entry))
        {
            entry = new Entry();
            _entries[aircraftId] = entry;
        }

        return entry;
    }

    private CrewRoster Roster(PersistentID aircraftId)
    {
        var entry = _entries.GetValueOrDefault(aircraftId);
        var wso = entry?.Wso;

        return new CrewRoster(
            aircraftId,
            wso != null ? wso.PlayerIndex : -1,
            wso != null ? _session.Economy.PendingOf(wso) : 0f,
            (sbyte)(entry?.WsoStation ?? -1),
            (sbyte)(entry?.PilotStation ?? -1)
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