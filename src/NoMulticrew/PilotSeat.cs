using NoMulticrew.Crew;
using NoMulticrew.Networking;
using UnityEngine;

namespace NoMulticrew;

internal sealed class PilotSeat
{
    private readonly CrewState _crew;

    public PilotSeat(CrewState crew)
    {
        _crew = crew;
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
            if (states.Get(i) && _crew.BlocksStation(aircraft, i))
            {
                stations[i].RemoteFireAuto(aircraft);
            }
        }
    }

    private void KeepOffCrewStations(Aircraft aircraft)
    {
        var current = aircraft.weaponManager.currentWeaponStation;
        if (current == null || !_crew.BlocksStation(aircraft, current.Number))
        {
            return;
        }

        var count = aircraft.weaponStations.Count;

        for (var step = 1; step < count; step++)
        {
            var candidate = (current.Number + step) % count;
            if (_crew.BlocksStation(aircraft, candidate))
            {
                continue;
            }

            aircraft.SetActiveStation((byte)candidate);
            SceneSingleton<CombatHUD>.i.ShowWeaponStation(aircraft.weaponStations[candidate]);
            return;
        }
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
            && GameManager.IsLocalAircraft(aircraft)
            && _crew.BlocksStation(aircraft, station))
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
