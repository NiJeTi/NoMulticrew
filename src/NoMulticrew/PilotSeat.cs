using NoMulticrew.Crew;
using NoMulticrew.Networking;
using UnityEngine;

namespace NoMulticrew;

internal sealed class PilotSeat
{
    private const float AimForwardIntervalSeconds = 0.2f;

    private readonly CrewState _crew;

    private readonly Dictionary<byte, PersistentID> _triggers = [];

    private readonly Dictionary<byte, float> _aimForwardedAt = [];

    private Aircraft? _aircraft;

    public PilotSeat(CrewState crew)
    {
        _crew = crew;
    }

    public void OnAction(CrewAction message)
    {
        if (!UnitRegistry.TryGetUnit<Aircraft>(message.AircraftId, out var aircraft)
            || !GameManager.IsLocalAircraft(aircraft))
        {
            Plugin.Logger.LogDebug($"Dropped relayed {message.Kind} for {message.AircraftId}, which this client does not fly");
            return;
        }

        switch (message.Kind)
        {
            case CrewActionKind.Radar when _crew.BlocksSensors(aircraft):
                Plugin.Logger.LogDebug("Relayed radar toggle");
                Relay(aircraft.CmdToggleRadar);
                break;
            case CrewActionKind.Trigger when _crew.BlocksStation(aircraft, message.StationIndex):
                SetTrigger(aircraft, message);
                break;
            case CrewActionKind.Aim when _crew.BlocksStation(aircraft, message.StationIndex):
                Aim(aircraft, message.StationIndex, message.Vector);
                break;
            default:
                Plugin.Logger.LogDebug($"Dropped relayed {message.Kind} for station {message.StationIndex}: not crewed here");
                break;
        }
    }

    public void Tick()
    {
        if (_triggers.Count == 0)
        {
            return;
        }

        var aircraft = _aircraft;
        if (aircraft == null || !GameManager.IsLocalAircraft(aircraft))
        {
            Clear();
            return;
        }

        foreach (var station in _triggers.Keys.ToList())
        {
            if (!_crew.BlocksStation(aircraft, station))
            {
                _triggers.Remove(station);
            }
        }

        _crew.Relaying = true;

        try
        {
            foreach (var (station, target) in _triggers)
            {
                Fire(aircraft, aircraft.weaponStations[station], target);
            }
        }
        finally
        {
            _crew.Relaying = false;
        }
    }

    public void Clear()
    {
        _triggers.Clear();
        _aimForwardedAt.Clear();
        _aircraft = null;
    }

    private void Aim(Aircraft aircraft, byte station, Vector3 vector)
    {
        if (!float.IsFinite(vector.x) || !float.IsFinite(vector.y) || !float.IsFinite(vector.z))
        {
            return;
        }

        aircraft.weaponStations[station].SetTurretVector(vector);

        var now = Time.timeSinceLevelLoad;
        if (now - _aimForwardedAt.GetValueOrDefault(station, float.NegativeInfinity) < AimForwardIntervalSeconds)
        {
            return;
        }

        _aimForwardedAt[station] = now;
        aircraft.SetTurretVector(station, vector);
    }

    private void SetTrigger(Aircraft aircraft, CrewAction message)
    {
        if (!ReferenceEquals(aircraft, _aircraft))
        {
            Clear();
            _aircraft = aircraft;
        }

        Plugin.Logger.LogDebug(
            $"Relayed trigger {(message.Held ? "held" : "released")} on station {message.StationIndex}"
        );

        if (message.Held)
        {
            _triggers[message.StationIndex] = message.TargetId;
        }
        else
        {
            _triggers.Remove(message.StationIndex);
        }
    }

    private static void Fire(Aircraft aircraft, WeaponStation station, PersistentID targetId)
    {
        if (station.SafetyIsOn(aircraft) || aircraft.remoteSim || !station.Ready() || station.SalvoInProgress)
        {
            return;
        }

        UnitRegistry.TryGetUnit(targetId, out var target);

        var info = station.WeaponInfo;
        if (info.gun || info.fireInterval == 0f || info.sling)
        {
            station.Fire(aircraft, target);
        }
        else
        {
            station.LaunchMount(aircraft, target, aircraft.GlobalPosition() + aircraft.transform.forward * 50000f);
        }
    }

    private void Relay(Action action)
    {
        _crew.Relaying = true;

        try
        {
            action();
        }
        finally
        {
            _crew.Relaying = false;
        }
    }
}
