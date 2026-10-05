using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NoMulticrew.Networking;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Crew;

internal sealed class CrewState
{
    private const float RefusalIntervalSeconds = 2f;

    private readonly Dictionary<PersistentID, CrewRoster> _crews = [];

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
                    (id, i) => $"seat {i}={(id < 0 ? "empty" : id.ToString())}"
                )
            )
        );

        if (UnitRegistry.TryGetUnit<Aircraft>(message.AircraftId, out var aircraft))
        {
            ApplyTurrets(aircraft);
        }
    }

    public void Clear()
    {
        _crews.Clear();
        _lastRefusal = float.NegativeInfinity;
    }

    public bool IsTaken(PersistentID aircraftId, int seatIndex)
    {
        return _crews.TryGetValue(aircraftId, out var crew)
            && seatIndex < crew.Occupants.Length
            && crew.Occupants[seatIndex] >= 0;
    }

    public static bool IsManned(Aircraft aircraft, WeaponStation station)
    {
        if (!station.HasTurret())
        {
            return false;
        }

        var server = Plugin.Server;
        if (server != null)
        {
            return server.Crew.OccupantOwning(aircraft, station.Number) != null;
        }

        return Plugin.Client?.Crew.OwnerSeat(aircraft, station.Number) >= 0;
    }

    public static void ApplyTurrets(Aircraft aircraft)
    {
        var current = aircraft.weaponManager.currentWeaponStation;

        foreach (var station in aircraft.weaponStations)
        {
            if (station.HasTurret())
            {
                station.SetStationActive(aircraft, ReferenceEquals(station, current));
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
            && OwnerSeat(aircraft, stationIndex) >= 0;
    }

    public bool BlocksSensors(Unit unit)
    {
        return GameManager.IsLocalAircraft(unit) && _crews.ContainsKey(unit.persistentID);
    }

    public bool Refuse(string text)
    {
        Plugin.Logger.LogDebug($"Suppressed: {text}");

        if (Time.timeSinceLevelLoad - _lastRefusal >= RefusalIntervalSeconds)
        {
            _lastRefusal = Time.timeSinceLevelLoad;
            SceneSingleton<AircraftActionsReport>.i.ReportText(text, RefusalIntervalSeconds);
        }

        return false;
    }

    public static bool Owns(Aircraft aircraft, int seatIndex, int stationIndex)
    {
        var stations = aircraft.weaponStations;
        if (stationIndex < 0 || stationIndex >= stations.Count)
        {
            return false;
        }

        var weapon = stations[stationIndex].WeaponInfo;
        var seats = Plugin.SeatTable.SeatsFor(aircraft.definition.jsonKey);

        return weapon != null && seatIndex >= 0 && seatIndex < seats.Count && seats[seatIndex].Operates(weapon.name);
    }

    public static bool OwnsAny(Aircraft aircraft, int seatIndex)
    {
        for (var i = 0; i < aircraft.weaponStations.Count; i++)
        {
            if (Owns(aircraft, seatIndex, i))
            {
                return true;
            }
        }

        return false;
    }

    public int OwnerSeat(Aircraft aircraft, int stationIndex)
    {
        if (!_crews.TryGetValue(aircraft.persistentID, out var crew))
        {
            return -1;
        }

        for (var i = 0; i < crew.Occupants.Length; i++)
        {
            if (crew.Occupants[i] >= 0 && Owns(aircraft, i, stationIndex))
            {
                return i;
            }
        }

        return -1;
    }
}
