using System.Linq;
using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatTable
{
    private static readonly SeatDefinition[] EmptySeats = [];

    private static readonly Dictionary<string, SeatDefinition[]> DefaultConfig = new()
    {
        ["COIN"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0f, 0f, -0.95f))], // CI-22
        ["trainer"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0f, -0.02f, -1.28f))], // T/A-30
        ["VTOLTrainer1"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0f, 0.16f, -1.30f))], // VT-7
        ["UtilityHelo1"] = [new SeatDefinition(SeatRole.Gunner, new Vector3(-0.45f, 0f, -1.20f))], // UH-90
        ["AttackHelo1"] = [new SeatDefinition(SeatRole.Gunner, new Vector3(0f, -0.43f, 1.40f))], // SAH-46
        ["QuadVTOL1"] = [new SeatDefinition(SeatRole.Gunner, new Vector3(0f, 0f, -1.48f))], // VL-49
        ["EW1"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0.80f, 0f, 0f))], // EW-25
        ["Darkreach"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0.99f, 0f, 0f))], // SFB-81
        ["FastBomber1"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0.84f, 0f, 0f))], // AB-4
    };

    private readonly Dictionary<string, SeatDefinition[]> _config;

    public SeatTable(Dictionary<string, SeatDefinition[]> config)
    {
        _config = config;
    }

    public static SeatTable Load()
    {
        var config = new Dictionary<string, SeatDefinition[]>(DefaultConfig);

        var overrides = SeatTableFile.TryRead();
        if (overrides != null)
        {
            foreach (var (name, seats) in overrides._config)
            {
                config[name] = seats;
            }

            Plugin.Logger.LogInfo($"Loaded {overrides._config.Count} seat table overrides");
        }

        foreach (var (name, seats) in config)
        {
            Plugin.Logger.LogDebug($"Seats of {name}: {string.Join(", ", seats.Select(x => x.ToString()))}");
        }

        return new SeatTable(config);
    }

    public IReadOnlyList<SeatDefinition> SeatsFor(string name)
    {
        return _config.GetValueOrDefault(name, EmptySeats);
    }

    public void Audit()
    {
        DumpAuthoringData();
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