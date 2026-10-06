using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NoMulticrew.Networking;
using NoMulticrew.Patches;
using NoMulticrew.Seats;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Crew;

internal sealed class CrewState
{
    private const float RefusalIntervalSeconds = 2f;

    private readonly Dictionary<PersistentID, CrewRoster> _crews = [];
    private readonly Dictionary<PersistentID, Dictionary<byte, PersistentID[]>> _stationTargets = [];

    private float _lastRefusal = float.NegativeInfinity;

    public void Apply(CrewRoster message)
    {
        if (message.Occupants.All(id => id < 0))
        {
            _crews.Remove(message.AircraftId);
        }
        else
        {
            _crews[message.AircraftId] = message;
        }

        Plugin.Logger.LogDebug(
            $"Crew state for {message.AircraftId}: "
            + string.Join(
                ", ",
                message.Occupants.Select(
                    (id, i) => $"seat {i}={(id < 0 ? "empty" : id.ToString())} station {message.Selected[i]}"
                )
            )
            + $", pilot station {message.PilotStation}"
        );

        if (UnitRegistry.TryGetUnit<Aircraft>(message.AircraftId, out var aircraft))
        {
            ApplyTurrets(aircraft);
        }
    }

    public void Clear()
    {
        _crews.Clear();
        _stationTargets.Clear();
    }

    public bool IsTaken(PersistentID aircraftId, int seatIndex)
    {
        return _crews.TryGetValue(aircraftId, out var crew)
            && seatIndex < crew.Occupants.Length
            && crew.Occupants[seatIndex] >= 0;
    }

    public static void ApplyTurrets(Aircraft aircraft)
    {
        var current = aircraft.weaponManager.currentWeaponStation;
        var flown = aircraft.Player != null;

        foreach (var station in aircraft.weaponStations)
        {
            if (!station.HasTurret())
            {
                continue;
            }

            var manual = WeaponStation_SetStationActive.IsManned(aircraft, station)
                || (flown && ReferenceEquals(station, current));

            foreach (var turret in station.Turrets)
            {
                turret.SetManual(manual);
            }
        }
    }

    public bool TryGetCrew(PersistentID aircraftId, out CrewRoster crew)
    {
        return _crews.TryGetValue(aircraftId, out crew);
    }

    public bool TryGetLocalSeat([NotNullWhen(true)] out Aircraft? aircraft, out int seatIndex)
    {
        aircraft = null;
        seatIndex = -1;

        if (!GameManager.GetLocalPlayer<Player>(out var player))
        {
            return false;
        }

        foreach (var (aircraftId, crew) in _crews)
        {
            var index = Array.IndexOf(crew.Occupants, player.PlayerIndex);

            if (index < 0 || !UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var found) || found.disabled)
            {
                continue;
            }

            aircraft = found;
            seatIndex = index;

            return true;
        }

        return false;
    }

    public bool BlocksStation(Unit unit, int stationIndex)
    {
        return unit is Aircraft aircraft
            && GameManager.IsLocalAircraft(aircraft)
            && Holder(aircraft, stationIndex) != SeatTable.Pilot;
    }

    public void RecordTargets(Unit unit, byte station, ReadOnlySpan<PersistentID> targets)
    {
        if (unit is not Aircraft aircraft || Plugin.SeatTable.SeatsFor(aircraft.definition.jsonKey).Count == 0)
        {
            return;
        }

        if (!_stationTargets.TryGetValue(aircraft.persistentID, out var stations))
        {
            stations = [];
            _stationTargets[aircraft.persistentID] = stations;
        }

        stations[station] = targets.ToArray();
    }

    public void CollectCrewmateTargets(Aircraft aircraft, int localSeat, HashSet<PersistentID> into)
    {
        into.Clear();

        if (!_crews.ContainsKey(aircraft.persistentID)
            || !_stationTargets.TryGetValue(aircraft.persistentID, out var stations))
        {
            return;
        }

        foreach (var (station, targets) in stations)
        {
            if (Holder(aircraft, station) != localSeat)
            {
                into.UnionWith(targets);
            }
        }
    }

    public IReadOnlyList<PersistentID> TargetsOf(Aircraft aircraft, int station)
    {
        return station >= 0
            && _stationTargets.TryGetValue(aircraft.persistentID, out var stations)
            && stations.TryGetValue((byte)station, out var targets)
                ? targets
                : Array.Empty<PersistentID>();
    }

    public bool Refuse(string text)
    {
        Plugin.Logger.LogDebug($"Suppressed: {text}");

        if (Time.unscaledTime - _lastRefusal >= RefusalIntervalSeconds)
        {
            _lastRefusal = Time.unscaledTime;
            SceneSingleton<AircraftActionsReport>.i.ReportText(text, RefusalIntervalSeconds);
        }

        return false;
    }

    public bool RefuseStation(Aircraft aircraft, int stationIndex)
    {
        return Refuse($"{SeatTable.Label(Holder(aircraft, stationIndex))} has this weapon");
    }

    public bool RefuseSelection(Aircraft aircraft, int station)
    {
        var holder = SeatTable.Label(Holder(aircraft, station));

        return Refuse(Plugin.SeatTable.IsShared(aircraft) ? $"{holder} is using this weapon" : $"{holder}-only weapon");
    }

    public SeatState StateOf(Aircraft aircraft)
    {
        var local = GameManager.IsLocalAircraft(aircraft);
        var own = local ? aircraft.weaponManager.currentWeaponStation?.Number ?? -1 : -1;

        if (!_crews.TryGetValue(aircraft.persistentID, out var crew))
        {
            return new SeatState(false, -1, own);
        }

        return new SeatState(
            crew.Occupants[SeatTable.Wso] >= 0,
            SeatTable.StationIndex(crew.Selected[SeatTable.Wso]),
            local ? own : SeatTable.StationIndex(crew.PilotStation)
        );
    }

    public int Holder(Aircraft aircraft, int station)
    {
        return Plugin.SeatTable.Holder(aircraft, station, StateOf(aircraft));
    }

    public bool CanSelect(Aircraft aircraft, int seat, int station)
    {
        return Plugin.SeatTable.CanSelect(aircraft, seat, station, StateOf(aircraft));
    }
}
