using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatTable
{
    private static readonly SeatDefinition[] EmptySeats = [];

    private static readonly Dictionary<string, SeatDefinition[]> Config = new()
    {
        ["COIN"] =
        [
            new SeatDefinition(
                SeatRole.Wso,
                weapons:
                [
                    "info_AGM1",
                    "info_AGM_heavy",
                    "info_AGM_scanner1",
                    "info_bomb_125_1",
                    "info_bomb_250_1",
                    "info_rocket1",
                    "info_rocket2",
                ],
                views: [new SeatView(new Vector3(0f, 0f, -0.95f))]
            ),
        ],
        ["trainer"] =
        [
            new SeatDefinition(
                SeatRole.Wso,
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
                    "info_rocket1",
                    "info_rocket2",
                ],
                views: [new SeatView(new Vector3(0f, -0.02f, -1.28f))]
            ),
        ],
        ["VTOLTrainer1"] =
        [
            new SeatDefinition(
                SeatRole.Wso,
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
                    "info_rocket1",
                    "info_rocket2",
                ],
                views: [new SeatView(new Vector3(0f, 0.16f, -1.30f))]
            ),
        ],
        ["UtilityHelo1"] =
        [
            new SeatDefinition(
                SeatRole.Gunner,
                weapons: ["Grenade_40mm", "Gun12.7mm", "Gun25mm_Autocannon"],
                views:
                [
                    new SeatView(["Left Fuselage Pylon"], new Vector3(-0.83f, -0.82f, -1.87f)),
                    new SeatView(["Right Fuselage Pylon"], new Vector3(1.42f, -0.82f, -1.87f)),
                    new SeatView(["Left Fuselage Pylon", "Right Fuselage Pylon"], new Vector3(0f, -0.67f, -1.87f)),
                    new SeatView(["Door guns"], new Vector3(0.29f, -0.47f, -1.19f)),
                    new SeatView(new Vector3(-0.45f, 0f, -1.20f)),
                ]
            ),
        ],
        ["AttackHelo1"] =
        [
            new SeatDefinition(
                SeatRole.Gunner,
                weapons: ["Gun25mm_Autocannon"],
                views:
                [
                    new SeatView(["Stub Pylons"], new Vector3(0f, -1.11f, -2.44f)),
                    new SeatView(new Vector3(0f, -0.43f, 1.40f)),
                ]
            ),
        ],
        ["QuadVTOL1"] =
        [
            new SeatDefinition(
                SeatRole.Wso,
                weapons: ["info_AGM1", "info_AGM_scanner1", "info_bomb_demolition"],
                views: [new SeatView(new Vector3(0f, 0f, -1.48f))]
            ),
            new SeatDefinition(
                SeatRole.Gunner,
                weapons: ["Gun12.7mm_Rotary", "Gun25mm_Autocannon"],
                views:
                [
                    new SeatView(["Floor Turret Mount"], new Vector3(0.40f, -1.90f, -2.60f)),
                    new SeatView(["Left Sponson Pylon"], new Vector3(-1.54f, -2.04f, -7.02f)),
                    new SeatView(["Right Sponson Pylon"], new Vector3(2.34f, -2.04f, -7.02f)),
                    new SeatView(["Left Sponson Pylon", "Right Sponson Pylon"], new Vector3(0f, -1.89f, -7.02f)),
                    new SeatView(new Vector3(0f, 0f, -1.48f)),
                ]
            ),
            new SeatDefinition(
                SeatRole.Gunner,
                weapons: ["Gun57mm_Aerial", "Gun76mm_Guided"],
                views:
                [
                    new SeatView(["Cargo Bay (Front)"], new Vector3(0.40f, -2.07f, -9.72f)),
                    new SeatView(new Vector3(0f, 0f, -1.48f)),
                ]
            ),
        ],
        ["EW1"] =
        [
            new SeatDefinition(
                SeatRole.Wso,
                weapons:
                [
                    "ARM1_info",
                    "AShM2_info",
                    "JammingPod1",
                    "info_bomb_250_glide",
                    "info_bomb_500_glide",
                    "info_bomb_glide1",
                ],
                views: [new SeatView(new Vector3(0.80f, 0f, 0f))]
            ),
        ],
        ["Darkreach"] =
        [
            new SeatDefinition(
                SeatRole.Wso,
                weapons:
                [
                    "AShM2_info",
                    "AShM3_info",
                    "ballisticMissile1_info",
                    "ballisticMissile1_tacNuke_info",
                    "info_AShM1",
                    "info_CruiseMissile1",
                    "info_CruiseMissile20kt",
                    "info_blastFrag500",
                    "info_bomb_250_1",
                    "info_bomb_250_glide",
                    "info_bomb_500_glide",
                    "info_bomb_demolition",
                    "info_bomb_penetrator1",
                    "info_nuclearBomb1",
                    "info_nuclearBomb1_strategic",
                ],
                views: [new SeatView(new Vector3(0.99f, 0f, 0f))]
            ),
        ],
        ["FastBomber1"] =
        [
            new SeatDefinition(
                SeatRole.Wso,
                weapons:
                [
                    "ARM1_info",
                    "AShM3_info",
                    "JammingPod1",
                    "ballisticMissile1_info",
                    "ballisticMissile1_tacNuke_info",
                    "info_AShM1",
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

    public SeatTable()
    {
        foreach (var (name, seats) in Config)
        {
            _config[name] = Checked(name, seats);
        }
    }

    public IReadOnlyList<SeatDefinition> SeatsFor(string name)
    {
        return _config.GetValueOrDefault(name, EmptySeats);
    }

    public string Label(string name, int seatIndex)
    {
        var seats = SeatsFor(name);
        if (seatIndex < 0 || seatIndex >= seats.Count)
        {
            return "Crew";
        }

        var role = seats[seatIndex].Role;
        var text = role == SeatRole.Wso ? "WSO" : role.ToString();

        if (seats.Count(x => x.Role == role) < 2)
        {
            return text;
        }

        return $"{text} {seats.Take(seatIndex + 1).Count(x => x.Role == role)}";
    }

    public void Audit()
    {
        var known = Resources.FindObjectsOfTypeAll<WeaponInfo>().Select(x => x.name).ToHashSet();

        foreach (var (name, seats) in _config)
        {
            for (var i = 0; i < seats.Length; i++)
            {
                foreach (var weapon in seats[i].Weapons.Where(x => !known.Contains(x)))
                {
                    Plugin.Logger.LogError($"Seat {i} of {name} lists '{weapon}', which matches no WeaponInfo");
                }
            }
        }

        DumpAuthoringData();
    }

    private static SeatDefinition[] Checked(string name, SeatDefinition[] seats)
    {
        var claimed = new Dictionary<string, int>();
        var result = new SeatDefinition[seats.Length];

        for (var i = 0; i < seats.Length; i++)
        {
            var seat = seats[i];

            if (!seat.HasOneDefaultView)
            {
                Plugin.Logger.LogError($"Seat {i} of {name} needs exactly one default view");
            }

            if (seat.Weapons.Count == 0)
            {
                Plugin.Logger.LogError($"Seat {i} of {name} lists no weapons");
            }

            var duplicates = seat.Weapons.Where(claimed.ContainsKey).ToList();

            foreach (var weapon in duplicates)
            {
                Plugin.Logger.LogError(
                    $"'{weapon}' is listed by seats {claimed[weapon]} and {i} of {name}; seat {claimed[weapon]} keeps it"
                );
            }

            foreach (var weapon in seat.Weapons.Except(duplicates))
            {
                claimed[weapon] = i;
            }

            result[i] = duplicates.Count == 0
                ? seat
                : new SeatDefinition(seat.Role, [.. seat.Weapons.Except(duplicates)], [.. seat.Views]);

            Plugin.Logger.LogDebug($"Seat {i} of {name}: {result[i]}");
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

            var gunner = new SortedSet<string>();
            var wso = new SortedSet<string>();

            Plugin.Logger.LogDebug($"=== Seat authoring: {definition.jsonKey} ({definition.unitName}) ===");

            foreach (var set in aircraft.weaponManager.hardpointSets)
            {
                var points = set.hardpoints
                    .Where(x => x != null && x.transform != null)
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
                    var ground = info.effectiveness.antiSurface + info.effectiveness.antiRadar;
                    var air = info.effectiveness.antiAir + info.effectiveness.antiMissile;

                    Plugin.Logger.LogDebug(
                        $"  '{info.name}' ({info.weaponName}) mount={mount.jsonKey} turret={mount.turret} "
                        + $"gun={info.gun} jammer={info.jammer} ground={ground:F2} air={air:F2}"
                    );

                    if (mount.turret)
                    {
                        gunner.Add(info.name);
                    }
                    else if (!info.gun && !info.cargo && !info.troops && !info.sling && (info.jammer || ground > air))
                    {
                        wso.Add(info.name);
                    }
                }
            }

            Plugin.Logger.LogDebug($"suggested Gunner weapons: {Quote(gunner)}");
            Plugin.Logger.LogDebug($"suggested Wso weapons: {Quote(wso)}");
        }
    }

    private static string Quote(IEnumerable<string> names)
    {
        return string.Join(", ", names.Select(x => $"\"{x}\""));
    }
}
