using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatTable
{
    public const int Pilot = -1;
    public const int Wso = 0;
    public const byte NoStation = 255;

    private static readonly SeatDefinition[] EmptySeats = [];

    private static readonly Dictionary<string, SeatDefinition[]> Config = new()
    {
        ["COIN"] =
        [
            new SeatDefinition(
                weapons:
                [
                    "info_AGM1",
                    "info_AGM_heavy",
                    "info_AGM_scanner1",
                    "info_bomb_125_1",
                    "info_bomb_250_1",
                    "Gun20mm_Rotary_Turret",
                ],
                views: [new SeatView(new Vector3(0f, 0f, -0.95f))]
            ),
        ],
        ["trainer"] =
        [
            new SeatDefinition(
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
                views: [new SeatView(new Vector3(0f, -0.02f, -1.28f))]
            ),
        ],
        ["VTOLTrainer1"] =
        [
            new SeatDefinition(
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
                views: [new SeatView(new Vector3(0f, 0.16f, -1.30f))]
            ),
        ],
        ["UtilityHelo1"] =
        [
            new SeatDefinition(
                weapons: ["info_AGM1", "info_AGM_scanner1", "GTG1_info", "Grenade_40mm", "Gun12.7mm"],
                views:
                [
                    new SeatView(["Door guns"], new Vector3(0.29f, -0.47f, -1.19f)),
                    new SeatView(new Vector3(-0.45f, 0f, -1.20f)),
                ]
            ),
        ],
        ["AttackHelo1"] =
        [
            new SeatDefinition(
                weapons: ["info_AGM1", "info_AGM2", "info_AGM_heavy", "info_AGM_scanner1", "Gun30mm_Rotary_Turret"],
                views: [new SeatView(new Vector3(0f, -0.43f, 1.40f))]
            ),
        ],
        ["QuadVTOL1"] =
        [
            new SeatDefinition(
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
                views:
                [
                    new SeatView(["Floor Turret Mount"], new Vector3(0.40f, -1.90f, -2.60f)),
                    new SeatView(["Left Sponson Pylon"], new Vector3(-1.54f, -2.04f, -7.02f)),
                    new SeatView(["Right Sponson Pylon"], new Vector3(2.34f, -2.04f, -7.02f)),
                    new SeatView(["Left Sponson Pylon", "Right Sponson Pylon"], new Vector3(0f, -1.89f, -7.02f)),
                    new SeatView(["Cargo Bay (Front)"], new Vector3(0.40f, -2.07f, -9.72f)),
                    new SeatView(new Vector3(0f, 0f, -1.48f)),
                ]
            ),
        ],
        ["EW1"] =
        [
            new SeatDefinition(
                weapons:
                [
                    "JammingPod1",
                    "ARM1_info",
                    "AShM2_info",
                    "info_bomb_250_glide",
                    "info_bomb_500_glide",
                    "info_bomb_glide1",
                ],
                views: [new SeatView(new Vector3(0.80f, 0f, 0f))]
            ),
        ],
        ["Darkreach"] =
        [
            SeatDefinition.Shared(views: [new SeatView(new Vector3(0.99f, 0f, 0f))]),
        ],
        ["FastBomber1"] =
        [
            new SeatDefinition(
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
                views: [new SeatView(new Vector3(0.84f, 0f, 0f))]
            ),
        ],
    };

    private readonly Dictionary<string, SeatDefinition[]> _config = [];

    private bool _audited;

    public SeatTable()
    {
        foreach (var (name, seats) in Config)
        {
            _config[name] = Checked(name, seats);
        }
    }

    public static string Label(int seat)
    {
        return seat == Pilot ? "Pilot" : "WSO";
    }

    public static int StationIndex(byte station)
    {
        return station == NoStation ? -1 : station;
    }

    public IReadOnlyList<SeatDefinition> SeatsFor(string name)
    {
        return _config.GetValueOrDefault(name, EmptySeats);
    }

    public bool IsShared(Aircraft aircraft)
    {
        var seats = SeatsFor(aircraft.definition.jsonKey);

        return seats.Count > 0 && seats[Wso].IsShared;
    }

    public bool Offered(Aircraft aircraft, int seatIndex)
    {
        var seats = SeatsFor(aircraft.definition.jsonKey);
        if (seatIndex < 0 || seatIndex >= seats.Count)
        {
            return false;
        }

        var seat = seats[seatIndex];

        return seat.IsShared
            ? aircraft.weaponStations.Count > 0
            : aircraft.weaponStations.Any(x => seat.Operates(x.WeaponInfo.name));
    }

    public int Holder(Aircraft aircraft, int station, SeatState state)
    {
        var seats = SeatsFor(aircraft.definition.jsonKey);
        if (!state.WsoAboard || seats.Count == 0 || station < 0 || station >= aircraft.weaponStations.Count)
        {
            return Pilot;
        }

        var seat = seats[Wso];
        var held = seat.IsShared
            ? state.WsoStation == station
            : seat.Operates(aircraft.weaponStations[station].WeaponInfo.name);

        return held ? Wso : Pilot;
    }

    public bool CanSelect(Aircraft aircraft, int seat, int station, SeatState state)
    {
        if (station < 0 || station >= aircraft.weaponStations.Count)
        {
            return false;
        }

        var holder = Holder(aircraft, station, state);

        if (seat == Pilot || !IsShared(aircraft))
        {
            return holder == seat;
        }

        return holder == Wso || state.PilotStation != station;
    }

    public void Audit()
    {
        if (_audited)
        {
            return;
        }

        _audited = true;

        var known = Resources.FindObjectsOfTypeAll<WeaponInfo>().Select(x => x.name).ToHashSet();

        foreach (var (name, seats) in _config)
        {
            foreach (var seat in seats)
            {
                foreach (var weapon in seat.Weapons.Where(x => !known.Contains(x)))
                {
                    Plugin.Logger.LogError($"The WSO of {name} lists '{weapon}', which matches no WeaponInfo");
                }
            }
        }

        DumpAuthoringData();
    }

    private static SeatDefinition[] Checked(string name, SeatDefinition[] seats)
    {
        if (seats.Length > 1)
        {
            Plugin.Logger.LogError($"{name} lists {seats.Length} crew seats; only the first is used");
        }

        var result = seats.Take(1).ToArray();

        foreach (var seat in result)
        {
            if (!seat.HasOneDefaultView)
            {
                Plugin.Logger.LogError($"The WSO of {name} needs exactly one default view");
            }

            if (!seat.IsShared && seat.Weapons.Count == 0)
            {
                Plugin.Logger.LogError($"The WSO of {name} lists no weapons");
            }

            Plugin.Logger.LogDebug($"Seat of {name}: {seat}");
        }

        return result;
    }

    private static void DumpAuthoringData()
    {
        foreach (var definition in Resources.FindObjectsOfTypeAll<AircraftDefinition>().OrderBy(x => x.jsonKey))
        {
            var aircraft = definition.unitPrefab != null ? definition.unitPrefab.GetComponent<Aircraft>() : null;
            if (aircraft == null
                || aircraft.weaponManager == null
                || aircraft.cockpit == null
                || aircraft.cockpitViewPoint == null)
            {
                continue;
            }

            var cockpit = aircraft.cockpit.transform;
            var eye = cockpit.InverseTransformPoint(aircraft.cockpitViewPoint.position);

            Plugin.Logger.LogDebug($"=== Seat authoring: {definition.jsonKey} ({definition.unitName}) ===");

            foreach (var set in aircraft.weaponManager.hardpointSets)
            {
                var points = set.hardpoints
                    .Where(x => x != null)
                    .Select(x => cockpit.InverseTransformPoint(x.transform.position) - eye)
                    .ToList();

                var centre = points.Count > 0
                    ? points.Aggregate(Vector3.zero, (sum, point) => sum + point) / points.Count
                    : Vector3.zero;

                Plugin.Logger.LogDebug(
                    $"set '{set.name}': {points.Count} hardpoints, centre {centre:F2} from the pilot's view point"
                );

                foreach (var mount in set.weaponOptions)
                {
                    if (mount == null || mount.info == null)
                    {
                        continue;
                    }

                    var info = mount.info;

                    Plugin.Logger.LogDebug(
                        $"  '{info.name}' ({info.weaponName}) mount={mount.jsonKey} turret={mount.turret} "
                        + $"gun={info.gun} laser={info.laserGuided} bomb={info.bomb} jammer={info.jammer}"
                    );
                }
            }
        }
    }
}
