using NoMulticrew.Networking;
using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew.Crew;

internal sealed class CrewState
{
    private const float RefusalIntervalSeconds = 2f;

    private readonly Dictionary<PersistentID, MulticrewState> _crews = [];

    private float _lastRefusal = float.NegativeInfinity;

    public void Apply(MulticrewState message)
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
                    (id, i) => $"seat {i}={(id < 0 ? "empty" : id.ToString())} role={message.Roles[i]}"
                )
            )
        );
    }

    public void Clear()
    {
        _crews.Clear();
    }

    public bool IsTaken(PersistentID aircraftId, int seatIndex)
    {
        return _crews.TryGetValue(aircraftId, out var crew)
               && seatIndex < crew.Occupants.Length
               && crew.Occupants[seatIndex] >= 0;
    }

    public bool BlocksStation(Unit unit, int stationIndex)
    {
        if (unit is not Aircraft aircraft
            || !GameManager.IsLocalAircraft(aircraft)
            || !_crews.TryGetValue(aircraft.persistentID, out var crew))
        {
            return false;
        }

        for (var i = 0; i < crew.Occupants.Length; i++)
        {
            if (crew.Occupants[i] >= 0 && Owns(crew.Roles[i], aircraft, stationIndex))
            {
                return true;
            }
        }

        return false;
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

    private static bool Owns(SeatRole role, Aircraft aircraft, int stationIndex)
    {
        var stations = aircraft.weaponStations;
        if (stationIndex < 0 || stationIndex >= stations.Count)
        {
            return false;
        }

        var station = stations[stationIndex];

        switch (role)
        {
            case SeatRole.Gunner:
                return station.HasTurret();
            case SeatRole.Wso:
                var weapon = station.WeaponInfo;

                if (weapon.gun || station.HasTurret())
                {
                    return false;
                }

                var ground = weapon.effectiveness.antiSurface + weapon.effectiveness.antiRadar;
                var air = weapon.effectiveness.antiAir + weapon.effectiveness.antiMissile;

                return ground > air;
            default:
                return false;
        }
    }
}
