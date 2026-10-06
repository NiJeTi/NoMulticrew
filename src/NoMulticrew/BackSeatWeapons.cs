using Cysharp.Threading.Tasks;
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
        var aircraft = _seat.Aircraft!;

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

    public void ReleaseAll()
    {
        var aircraft = _seat.Aircraft!;

        foreach (var station in _firing)
        {
            Stop(aircraft, station);
        }

        _firing.Clear();
    }

    public void SetFiring(int station, bool firing)
    {
        if (!firing || !_firing.Add((byte)station))
        {
            return;
        }

        _session.SendCommand(CrewCommand.FiringState(_seat.Aircraft!.persistentID, (byte)station, true));
    }

    public void ClaimHit(Unit hitUnit, Vector3 relativePos, Vector3 bulletVelocity, byte station)
    {
        _session.SendCommand(
            CrewCommand.ClaimHit(
                _seat.Aircraft!.persistentID,
                station,
                hitUnit.persistentID,
                NetworkFloatHelper.CompressIfValid(relativePos, logErrors: true, "relativePos"),
                NetworkFloatHelper.CompressIfValid(bulletVelocity, logErrors: true, "bulletVelocity")
            )
        );
    }

    public void SingleFire(byte station)
    {
        _session.SendCommand(CrewCommand.SingleFire(_seat.Aircraft!.persistentID, station));
    }

    public void PushTargets()
    {
        if (_seat.Station < 0)
        {
            return;
        }

        var aircraft = _seat.Aircraft!;
        var targets = aircraft.weaponManager.GetTargetList()
            .Where(x => x != null)
            .Take(128)
            .Select(x => x.persistentID)
            .ToArray();

        _session.SendCommand(CrewCommand.SetStationTargets(aircraft.persistentID, (byte)_seat.Station, targets));
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
        if (camera.currentState != camera.cockpitState)
        {
            return;
        }

        var vector = camera.transform.forward;
        turret.SetVector(vector);

        if (Time.unscaledTime - _aimSentAt <= AimIntervalSeconds)
        {
            return;
        }

        _aimSentAt = Time.unscaledTime;
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

        if (targets.Count > 1)
        {
            station.SalvoInProgress = true;
            Salvo(aircraft, station, targets, info.fireInterval * 1.1f).Forget();
            return;
        }

        Launch(aircraft, station, target, aircraft.GlobalPosition() + aircraft.transform.forward * 50000f);
    }

    private async UniTask Salvo(Aircraft aircraft, WeaponStation station, List<Unit> targets, float interval)
    {
        try
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (!ReferenceEquals(_seat.Aircraft, aircraft))
                {
                    return;
                }

                var unit = targets[i];
                if (unit != null && !unit.disabled)
                {
                    Launch(aircraft, station, unit, default);
                }

                await UniTask.Delay((int)(interval * 1000f));
            }
        }
        finally
        {
            station.SalvoInProgress = false;
        }
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
            Stop(aircraft, station);
        }
    }

    private void Stop(Aircraft aircraft, byte station)
    {
        _session.SendCommand(CrewCommand.FiringState(aircraft.persistentID, station, false));
        _session.SendCommand(CrewCommand.StoppedFiring(aircraft.persistentID, station));
    }
}
