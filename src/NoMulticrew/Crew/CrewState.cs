using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
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
                    (id, i) => $"seat {i}={(id < 0 ? "empty" : id.ToString())} role={message.Roles[i]}"
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

    public bool HasGunner(PersistentID aircraftId)
    {
        if (!_crews.TryGetValue(aircraftId, out var crew))
        {
            return false;
        }

        for (var i = 0; i < crew.Occupants.Length; i++)
        {
            if (crew.Occupants[i] >= 0 && crew.Roles[i] == SeatRole.Gunner)
            {
                return true;
            }
        }

        return false;
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
            return server.Crew.HasGunner(aircraft.persistentID);
        }

        return Plugin.Client?.Crew.HasGunner(aircraft.persistentID) == true;
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

    public static bool OwnsAny(SeatRole role, Aircraft aircraft)
    {
        for (var i = 0; i < aircraft.weaponStations.Count; i++)
        {
            if (Owns(role, aircraft, i))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Owns(SeatRole role, Aircraft aircraft, int stationIndex)
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
                return !station.HasTurret() && IsGroundWeapon(station.WeaponInfo);
            default:
                return false;
        }
    }

    public static bool IsGroundWeapon(WeaponInfo weapon)
    {
        var ground = weapon.effectiveness.antiSurface + weapon.effectiveness.antiRadar;
        var air = weapon.effectiveness.antiAir + weapon.effectiveness.antiMissile;

        return !weapon.gun && ground > air;
    }

    public static void LogClassification()
    {
        Plugin.Logger.LogDebug("=== Weapon classification ===");
        Plugin.Logger.LogDebug("seat | weapon | surf | radar | air | msl | gun");

        foreach (var weapon in Resources.FindObjectsOfTypeAll<WeaponInfo>().OrderBy(x => x.weaponName))
        {
            var e = weapon.effectiveness;

            Plugin.Logger.LogDebug(
                $"{(IsGroundWeapon(weapon) ? "WSO  " : "PILOT")} | {weapon.weaponName} | "
                + $"{e.antiSurface:F2} | {e.antiRadar:F2} | {e.antiAir:F2} | {e.antiMissile:F2} | {weapon.gun}"
            );
        }

        Plugin.Logger.LogDebug("=== end weapon classification ===");
    }
}