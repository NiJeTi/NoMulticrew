using System.Diagnostics.CodeAnalysis;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Crew;

internal sealed class CrewState
{
    private const float RefusalIntervalSeconds = 2f;

    private readonly Dictionary<PersistentID, CrewRoster> _rosters = [];
    private readonly Dictionary<PersistentID, Dictionary<byte, PersistentID[]>> _stationTargets = [];

    private float _lastRefusal = float.NegativeInfinity;

    public void Apply(CrewRoster message)
    {
        if (message.WsoPlayerIndex >= 0)
        {
            _rosters[message.AircraftId] = message;
        }
        else
        {
            _rosters.Remove(message.AircraftId);
        }

        Plugin.Logger.LogDebug(
            $"Crew state for {message.AircraftId}: "
            + $"WSO {(message.WsoPlayerIndex < 0 ? "empty" : message.WsoPlayerIndex.ToString())} "
            + $"station {message.WsoStation}, pilot station {message.PilotStation}"
        );

        if (UnitRegistry.TryGetUnit<Aircraft>(message.AircraftId, out var aircraft))
        {
            Plugin.SeatTable.ApplyTurrets(aircraft);
        }
    }

    public void Clear()
    {
        _rosters.Clear();
        _stationTargets.Clear();
    }

    public bool HasRoster(PersistentID aircraftId)
    {
        return _rosters.ContainsKey(aircraftId);
    }

    public bool TryGetRoster(PersistentID aircraftId, out CrewRoster roster)
    {
        return _rosters.TryGetValue(aircraftId, out roster);
    }

    public static string NameOf(int playerIndex)
    {
        foreach (var player in UnitRegistry.playerLookup.Values)
        {
            if (player.PlayerIndex == playerIndex)
            {
                return player.GetDisplayName(PlayerNameContext.Other);
            }
        }

        return $"Player {playerIndex}";
    }

    public bool TryGetSeatedAircraft([NotNullWhen(true)] out Aircraft? aircraft)
    {
        aircraft = null;

        if (!GameManager.GetLocalPlayer<Player>(out var player))
        {
            return false;
        }

        foreach (var (aircraftId, roster) in _rosters)
        {
            if (roster.WsoPlayerIndex != player.PlayerIndex
                || !UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var found)
                || found.disabled)
            {
                continue;
            }

            aircraft = found;

            return true;
        }

        return false;
    }

    public void RecordTargets(Unit unit, byte station, ReadOnlySpan<PersistentID> targets)
    {
        if (unit is not Aircraft aircraft || Plugin.SeatTable.WsoSeat(aircraft) == null)
        {
            return;
        }

        if (!_stationTargets.TryGetValue(aircraft.persistentID, out var stations))
        {
            stations = [];
            _stationTargets[aircraft.persistentID] = stations;
        }

        stations[station] = targets.ToArray();

        Plugin.Logger.LogDebug($"Recorded {targets.Length} targets for station {station} of {aircraft.persistentID}");
    }

    public void CollectCrewmateTargets(Aircraft aircraft, Role local, HashSet<PersistentID> into)
    {
        into.Clear();

        if (!_rosters.ContainsKey(aircraft.persistentID)
            || !_stationTargets.TryGetValue(aircraft.persistentID, out var stations))
        {
            return;
        }

        foreach (var (station, targets) in stations)
        {
            if (RoleHolding(aircraft, station) != local)
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
        return Refuse($"{SeatTable.Label(RoleHolding(aircraft, stationIndex))} has this weapon");
    }

    public bool RefuseSelection(Aircraft aircraft, int station)
    {
        var holder = SeatTable.Label(RoleHolding(aircraft, station));

        return Refuse(Plugin.SeatTable.IsShared(aircraft) ? $"{holder} is using this weapon" : $"{holder}-only weapon");
    }

    public SeatState ClientState(Aircraft aircraft)
    {
        var local = GameManager.IsLocalAircraft(aircraft);
        var own = local ? aircraft.weaponManager.currentWeaponStation?.Number ?? -1 : -1;

        if (!_rosters.TryGetValue(aircraft.persistentID, out var roster))
        {
            return new SeatState(false, -1, own);
        }

        return new SeatState(
            true,
            SeatTable.StationIndex(roster.WsoStation),
            local ? own : SeatTable.StationIndex(roster.PilotStation)
        );
    }

    public Role RoleHolding(Aircraft aircraft, int station)
    {
        return Plugin.SeatTable.Holder(aircraft, station, ClientState(aircraft));
    }

    public bool CanSelect(Aircraft aircraft, Role role, int station)
    {
        return Plugin.SeatTable.CanSelect(aircraft, role, station, ClientState(aircraft));
    }

    public int NextSelectable(Aircraft aircraft, Role role, int from, int direction)
    {
        var count = aircraft.weaponStations.Count;

        for (var step = 1; step <= count; step++)
        {
            var candidate = ((from + direction * step) % count + count) % count;
            if (CanSelect(aircraft, role, candidate))
            {
                return candidate;
            }
        }

        return -1;
    }
}
