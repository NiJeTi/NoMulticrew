using NoMulticrew.Networking;
using UnityEngine;

namespace NoMulticrew;

internal sealed class BackSeatWeapons
{
    private const float AimIntervalSeconds = 0.2f;
    private const float FiringReleaseSeconds = 0.2f;

    private readonly ClientSession _session;
    private readonly BackSeat _seat;

    private readonly HashSet<byte> _firing = [];

    private float _aimSentAt = float.NegativeInfinity;

    public BackSeatWeapons(ClientSession session, BackSeat seat)
    {
        _session = session;
        _seat = seat;
    }

    public void Tick(bool triggerHeld)
    {
        var aircraft = _seat.Aircraft;
        if (aircraft == null)
        {
            return;
        }

        if (triggerHeld && _seat.Station < 0)
        {
            _session.Crew.Refuse("No weapons for this seat");
        }
        else if (triggerHeld)
        {
            Fire(aircraft, aircraft.weaponStations[_seat.Station]);
        }

        Release(aircraft);
    }

    public void Clear()
    {
        _firing.Clear();
        _aimSentAt = float.NegativeInfinity;
    }

    public void SetFiring(int station, bool firing)
    {
        var aircraft = _seat.Aircraft;
        if (aircraft == null || !firing || !_firing.Add((byte)station))
        {
            return;
        }

        _session.SendCommand(CrewCommand.FiringState(aircraft.persistentID, (byte)station, true));
    }

    public void ClaimHit(Unit hitUnit, Vector3 relativePos, Vector3 bulletVelocity, byte station)
    {
        var aircraft = _seat.Aircraft;
        if (aircraft == null)
        {
            return;
        }

        _session.SendCommand(
            CrewCommand.ClaimHit(
                aircraft.persistentID,
                station,
                hitUnit.persistentID,
                NetworkFloatHelper.CompressIfValid(relativePos, logErrors: true, "relativePos"),
                NetworkFloatHelper.CompressIfValid(bulletVelocity, logErrors: true, "bulletVelocity")
            )
        );
    }

    public void SingleFire(byte station)
    {
        var aircraft = _seat.Aircraft;
        if (aircraft != null)
        {
            _session.SendCommand(CrewCommand.SingleFire(aircraft.persistentID, station));
        }
    }

    public void Aim(Turret turret, Aircraft aircraft, WeaponStation station)
    {
        if (!ReferenceEquals(aircraft, _seat.Aircraft)
            || station.Number != _seat.Station
            || !_seat.Owns(aircraft, station.Number))
        {
            return;
        }

        var camera = SceneSingleton<CameraStateManager>.i;
        if (camera == null || camera.currentState != camera.cockpitState)
        {
            return;
        }

        var vector = camera.transform.forward;
        turret.SetVector(vector);

        if (Time.timeSinceLevelLoad - _aimSentAt <= AimIntervalSeconds)
        {
            return;
        }

        _aimSentAt = Time.timeSinceLevelLoad;
        _session.SendCommand(
            CrewCommand.TurretVector(
                aircraft.persistentID,
                station.Number,
                NetworkFloatHelper.CompressIfValid(vector, logErrors: true, "direction", Vector3.forward)
            )
        );
    }

    private void Fire(Aircraft aircraft, WeaponStation station)
    {
        if (station.SafetyIsOn(aircraft) || !station.Ready() || station.SalvoInProgress)
        {
            return;
        }

        var targets = aircraft.weaponManager.GetTargetList();
        var target = targets.Count == 0 ? null : targets[0];

        var info = station.WeaponInfo;
        if (info.gun || info.fireInterval == 0f || info.sling)
        {
            station.Fire(aircraft, target);
            return;
        }

        Launch(aircraft, station, target, aircraft.GlobalPosition() + aircraft.transform.forward * 50000f);
    }

    private void Launch(Aircraft aircraft, WeaponStation station, Unit? target, GlobalPosition aimpoint)
    {
        _session.SendCommand(
            CrewCommand.LaunchMissile(
                aircraft.persistentID,
                station.Number,
                target != null ? target.persistentID : PersistentID.None,
                aimpoint
            )
        );

        if (!Plugin.IsServer)
        {
            station.LaunchMount(aircraft, target, aimpoint);
        }
    }

    private void Release(Aircraft aircraft)
    {
        if (_firing.Count == 0)
        {
            return;
        }

        var now = Time.timeSinceLevelLoad;

        foreach (var station in _firing.ToList())
        {
            if (station < aircraft.weaponStations.Count
                && now - aircraft.weaponStations[station].LastFiredTime <= FiringReleaseSeconds)
            {
                continue;
            }

            _firing.Remove(station);
            _session.SendCommand(CrewCommand.FiringState(aircraft.persistentID, station, false));
            _session.SendCommand(CrewCommand.StoppedFiring(aircraft.persistentID, station));
        }
    }
}
