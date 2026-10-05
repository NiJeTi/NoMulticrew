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
}