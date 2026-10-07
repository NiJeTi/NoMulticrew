using NoMulticrew.Networking;
using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew;

internal sealed class PilotSeat
{
    private readonly ClientSession _session;

    public PilotSeat(ClientSession session)
    {
        _session = session;
    }

    public void Tick()
    {
        if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft.disabled)
        {
            return;
        }

        KeepOffCrewStations(aircraft);

        var states = aircraft.NetworkremoteWeaponStates;
        if (states.Mask == 0)
        {
            return;
        }

        var stations = aircraft.weaponStations;

        for (var i = 0; i < stations.Count; i++)
        {
            if (states.Get(i) && _session.Crew.RoleHolding(aircraft, i) == Role.Wso)
            {
                stations[i].RemoteFireAuto(aircraft);
            }
        }
    }

    private void KeepOffCrewStations(Aircraft aircraft)
    {
        var current = aircraft.weaponManager.currentWeaponStation;
        if (current == null || _session.Crew.CanSelect(aircraft, Role.Pilot, current.Number))
        {
            return;
        }

        Cycle(aircraft, 1);

        if (!ReferenceEquals(aircraft.weaponManager.currentWeaponStation, current))
        {
            _session.Crew.RefuseStation(aircraft, current.Number);
        }
    }

    public bool Cycle(Aircraft aircraft, int direction)
    {
        var manager = aircraft.weaponManager;
        var current = manager.currentWeaponStation;
        if (current == null || !GameManager.IsLocalAircraft(aircraft))
        {
            return false;
        }

        var count = aircraft.weaponStations.Count;

        for (var step = 1; step < count; step++)
        {
            var candidate = ((current.Number + direction * step) % count + count) % count;
            if (!_session.Crew.CanSelect(aircraft, Role.Pilot, candidate))
            {
                continue;
            }

            manager.currentWeaponStation = aircraft.weaponStations[candidate];
            aircraft.SetActiveStation((byte)candidate);
            SceneSingleton<CombatHUD>.i.ShowWeaponStation(manager.currentWeaponStation);

            return true;
        }

        return true;
    }

    public void OnTurretVector(CrewTurretVector message)
    {
        var aircraft = Flown(message.AircraftId, message.Station);
        if (aircraft == null)
        {
            return;
        }

        aircraft.weaponStations[message.Station].SetTurretVector(
            NetworkFloatHelper.DecompressIfValid(message.Direction, logErrors: true, "direction", Vector3.forward)
        );
    }

    public void OnLaunch(CrewLaunch message)
    {
        var aircraft = Flown(message.AircraftId, message.Station);
        if (aircraft == null)
        {
            return;
        }

        UnitRegistry.TryGetUnit(message.TargetId, out var target);

        aircraft.weaponStations[message.Station].LaunchMount(aircraft, target, message.Aimpoint);
    }

    private Aircraft? Flown(PersistentID aircraftId, byte station)
    {
        if (UnitRegistry.TryGetUnit<Aircraft>(aircraftId, out var aircraft)
            && _session.Crew.BlocksStation(aircraft, station))
        {
            return aircraft;
        }

        Plugin.Logger.LogWarning(
            $"Crew replay for station {station} of {aircraftId} reached a client that does not fly it "
            + "or whose crew does not own that station"
        );

        return null;
    }
}
