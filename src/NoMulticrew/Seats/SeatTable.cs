using System.Diagnostics.CodeAnalysis;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatTable
{
    public const byte NoStation = 255;

    private const float BoardingSpeed = 50f / 3.6f;
    private const float BoardingRadarAltitude = 5f;
    private const float ExitSpeed = 2f;

    private static readonly Dictionary<string, SeatDefinition> Config = new()
    {
        ["COIN"] = new SeatDefinition(
            weapons:
            [
                "info_AGM1",
                "info_AGM_heavy",
                "info_AGM_scanner1",
                "info_bomb_125_1",
                "info_bomb_250_1",
                "Gun20mm_Rotary_Turret",
            ],
            view: new Vector3(0f, 0f, -0.95f),
            screen: new ScreenPlacement(
                new Vector3(0f, 0.874f, -0.887f),
                new Vector3(0f, 0.342f, -0.94f),
                new Rect(0.001f, 0f, 0.79f, 1f)
            )
        ),
        ["trainer"] = new SeatDefinition(
            weapons:
            [
                "info_AGM1",
                "info_AGM_heavy",
                "info_Bomb_cluster1",
                "info_blastFrag500",
                "info_bomb_125_1",
                "info_bomb_125_HD",
                "info_bomb_250_1",
                "info_bomb_250_glide",
                "info_bomb_glide1",
                "info_nuclearBomb1",
                "info_nuclearBomb1_strategic",
            ],
            view: new Vector3(0f, -0.02f, -1.28f),
            screen: new ScreenPlacement(
                new Vector3(0f, 0.825f, -1.173f),
                new Vector3(0f, 0.259f, -0.966f),
                new Rect(0f, 0.291f, 0.615f, 0.709f)
            )
        ),
        ["VTOLTrainer1"] = new SeatDefinition(
            weapons:
            [
                "info_AGM1",
                "info_AGM_heavy",
                "info_Bomb_cluster1",
                "info_blastFrag500",
                "info_bomb_125_1",
                "info_bomb_250_1",
                "info_bomb_250_glide",
                "info_bomb_glide1",
                "info_nuclearBomb1",
                "info_nuclearBomb1_strategic",
            ],
            view: new Vector3(0f, 0.16f, -1.30f),
            screen: new ScreenPlacement(
                new Vector3(0f, 0.32f, -1.099f),
                new Vector3(0f, 0.342f, -0.94f),
                new Rect(0.001f, 0.287f, 0.614f, 0.712f)
            )
        ),
        ["UtilityHelo1"] = new SeatDefinition(
            weapons: ["info_AGM1", "info_AGM_scanner1", "GTG1_info", "Grenade_40mm", "Gun12.7mm"],
            view: new Vector3(0.98f, 0f, 0f),
            panel: new PanelPlacement(
                new Vector3(-0.455f, 0.468f, 0.716f),
                new Vector3(-0.176f, 0.468f, 0.716f),
                new Vector3(-0.176f, 0.269f, 0.695f),
                new Vector3(-0.455f, 0.269f, 0.695f),
                new Vector2(0f, 1f),
                new Vector2(0.525f, 0.249f),
                new Vector3(0f, 0.105f, -0.994f)
            )
        ),
        ["AttackHelo1"] = new SeatDefinition(
            weapons: ["info_AGM1", "info_AGM2", "info_AGM_heavy", "info_AGM_scanner1", "Gun30mm_Rotary_Turret"],
            view: new Vector3(0f, -0.43f, 1.40f),
            screen: new ScreenPlacement(
                new Vector3(0f, 0.023f, 1.929f),
                new Vector3(0f, 0.423f, -0.906f),
                new Rect(0f, 0.252f, 0.75f, 0.748f)
            )
        ),
        ["QuadVTOL1"] = new SeatDefinition(
            weapons:
            [
                "info_AGM1",
                "info_AGM_scanner1",
                "info_bomb_demolition",
                "Gun12.7mm_Rotary",
                "Gun25mm_Autocannon",
                "Gun57mm_Aerial",
                "Gun76mm_Guided",
            ],
            view: new Vector3(1.34f, 0f, 0f),
            panel: new PanelPlacement(
                new Vector3(-0.516f, 0.439f, 1.528f),
                new Vector3(-0.219f, 0.439f, 1.528f),
                new Vector3(-0.219f, 0.247f, 1.492f),
                new Vector3(-0.516f, 0.246f, 1.492f),
                new Vector2(0.001f, 0.995f),
                new Vector2(0.5f, 0.277f),
                new Vector3(0f, 0.184f, -0.983f)
            )
        ),
        ["EW1"] = new SeatDefinition(
            weapons:
            [
                "JammingPod1",
                "ARM1_info",
                "AShM2_info",
                "info_bomb_250_glide",
                "info_bomb_500_glide",
                "info_bomb_glide1",
            ],
            view: new Vector3(0.80f, 0f, 0f),
            panel: new PanelPlacement(
                new Vector3(-0.6383f, 0.344f, 0.4951f),
                new Vector3(-0.3463f, 0.344f, 0.4951f),
                new Vector3(-0.346f, 0.1801f, 0.4134f),
                new Vector3(-0.6382f, 0.1797f, 0.4132f),
                new Vector2(0f, 1f),
                new Vector2(0.56f, 0.254f),
                new Vector3(0f, 0.446f, -0.895f)
            )
        ),
        ["Darkreach"] = SeatDefinition.Shared(
            view: new Vector3(0.99f, 0f, 0f),
            panel: new PanelPlacement(
                new Vector3(-0.5794f, 1.1629f, 1.6931f),
                new Vector3(-0.2416f, 1.1628f, 1.6934f),
                new Vector3(-0.2412f, 0.9616f, 1.6268f),
                new Vector3(-0.5792f, 0.9612f, 1.6264f),
                new Vector2(0f, 1f),
                new Vector2(0.574f, 0.28f),
                new Vector3(0f, 0.314f, -0.949f)
            )
        ),
        ["FastBomber1"] = new SeatDefinition(
            weapons:
            [
                "JammingPod1",
                "ARM1_info",
                "AShM3_info",
                "info_AShM1",
                "ballisticMissile1_info",
                "ballisticMissile1_tacNuke_info",
                "info_blastFrag500",
                "info_bomb_250_1",
                "info_bomb_250_glide",
                "info_bomb_500_glide",
                "info_bomb_demolition",
                "info_bomb_penetrator1",
                "info_nuclearBomb1",
                "info_nuclearBomb1_strategic",
            ],
            view: new Vector3(0.84f, 0f, 0f),
            panel: new PanelPlacement(
                new Vector3(-0.666f, 0.0694f, 0.5823f),
                new Vector3(-0.2767f, 0.0695f, 0.5823f),
                new Vector3(-0.2767f, -0.1572f, 0.5527f),
                new Vector3(-0.666f, -0.1572f, 0.5527f),
                new Vector2(0.003f, 0.954f),
                new Vector2(0.572f, 0.285f),
                new Vector3(0f, 0.13f, -0.992f)
            )
        ),
    };

    private bool _audited;

    public SeatTable()
    {
        foreach (var (name, seat) in Config)
        {
            if (!seat.IsShared && seat.Weapons.Count == 0)
            {
                Plugin.Logger.LogError($"The WSO of {name} lists no weapons");
            }

            Plugin.Logger.LogDebug($"Seat of {name}: {seat}");
        }
    }

    public static string Label(Role role)
    {
        return role == Role.Pilot ? "Pilot" : "WSO";
    }

    public static int StationIndex(byte station)
    {
        return station == NoStation ? -1 : station;
    }

    public static int StationOf(Unit unit, WeaponInfo weapon)
    {
        for (var i = 0; i < unit.weaponStations.Count; i++)
        {
            if (unit.weaponStations[i].WeaponInfo == weapon)
            {
                return i;
            }
        }

        return -1;
    }

    public static bool CanBoard(
        Aircraft aircraft,
        Player joiner,
        [NotNullWhen(true)] out Airbase? airbase,
        [NotNullWhen(false)] out string? reason
    )
    {
        airbase = null;

        if (aircraft.disabled || aircraft.Player == null)
        {
            reason = "That aircraft has no pilot";
            return false;
        }

        if (aircraft.NetworkHQ == null || joiner.HQ != aircraft.NetworkHQ)
        {
            reason = "That aircraft belongs to the opposing faction";
            return false;
        }

        if (joiner.Aircraft != null)
        {
            reason = "You need to leave your aircraft first";
            return false;
        }

        if (!(aircraft.radarAlt < BoardingRadarAltitude
                && aircraft.speed < BoardingSpeed
                && aircraft.NetworkHQ.AnyNearAirbase(aircraft.transform.position, out airbase)))
        {
            reason = "The aircraft is not at an airbase";
            return false;
        }

        reason = null;
        return true;
    }

    public static bool IsValidExit(Aircraft aircraft)
    {
        return aircraft.speed < ExitSpeed
            && aircraft.NetworkHQ != null
            && aircraft.NetworkHQ.AnyNearAirbase(aircraft.transform.position, out _)
            && aircraft.transform.position.y > Datum.LocalSeaY;
    }

    public SeatDefinition? WsoSeat(Aircraft aircraft)
    {
        return Config.GetValueOrDefault(aircraft.definition.jsonKey);
    }

    public bool IsShared(Aircraft aircraft)
    {
        return WsoSeat(aircraft) is { IsShared: true };
    }

    public PanelPlacement? PanelOf(Aircraft aircraft)
    {
        return WsoSeat(aircraft)?.Panel;
    }

    public bool Offered(Aircraft aircraft)
    {
        var seat = WsoSeat(aircraft);
        if (seat == null)
        {
            return false;
        }

        return seat.IsShared
            ? aircraft.weaponStations.Count > 0
            : aircraft.weaponStations.Any(x => seat.Operates(x.WeaponInfo.name));
    }

    public Role Holder(Aircraft aircraft, int station, SeatState state)
    {
        var seat = WsoSeat(aircraft);
        if (!state.WsoAboard || seat == null || station < 0 || station >= aircraft.weaponStations.Count)
        {
            return Role.Pilot;
        }

        var held = seat.IsShared
            ? state.WsoStation == station
            : seat.Operates(aircraft.weaponStations[station].WeaponInfo.name);

        return held ? Role.Wso : Role.Pilot;
    }

    public bool CanSelect(Aircraft aircraft, Role role, int station, SeatState state)
    {
        if (station < 0 || station >= aircraft.weaponStations.Count)
        {
            return false;
        }

        var holder = Holder(aircraft, station, state);

        if (role == Role.Pilot || !IsShared(aircraft))
        {
            return holder == role;
        }

        return holder == Role.Wso || state.PilotStation != station;
    }

    public bool IsManned(Aircraft aircraft, WeaponStation station, SeatState state)
    {
        return station.HasTurret() && Holder(aircraft, station.Number, state) == Role.Wso;
    }

    public void ApplyTurrets(Aircraft aircraft, SeatState state)
    {
        var current = aircraft.weaponManager.currentWeaponStation;
        var flown = aircraft.Player != null;

        foreach (var station in aircraft.weaponStations)
        {
            if (!station.HasTurret())
            {
                continue;
            }

            var manual = IsManned(aircraft, station, state) || (flown && ReferenceEquals(station, current));

            foreach (var turret in station.Turrets)
            {
                turret.SetManual(manual);
            }
        }
    }

    public void Audit()
    {
        if (_audited)
        {
            return;
        }

        _audited = true;

        var known = Resources.FindObjectsOfTypeAll<WeaponInfo>().Select(x => x.name).ToHashSet();

        foreach (var (name, seat) in Config)
        {
            foreach (var weapon in seat.Weapons.Where(x => !known.Contains(x)))
            {
                Plugin.Logger.LogError($"The WSO of {name} lists '{weapon}', which matches no WeaponInfo");
            }
        }
    }
}
