using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatTable
{
    private static readonly SeatDefinition[] EmptySeats = [];

    private static readonly Dictionary<string, SeatDefinition[]> DefaultConfig = new()
    {
        ["COIN"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0f, -0.1f, -1.4f))], // CI-22
        ["trainer"] = EmptySeats, // T/A-30
        ["VTOLTrainer1"] = EmptySeats, // VT-7
        ["UtilityHelo1"] = EmptySeats, // UH-90
        ["AttackHelo1"] = [new SeatDefinition(SeatRole.Gunner, new Vector3(0f, -0.1f, -1.5f))], // SAH-46
        ["QuadVTOL1"] = [new SeatDefinition(SeatRole.Gunner, new Vector3(-1.1f, -0.3f, -2.0f))], // VL-49
        ["EW1"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0f, -0.1f, -1.6f))], // EW-25
        ["Darkreach"] = EmptySeats, // SFB-81
        ["FastBomber1"] = EmptySeats, // AB-4
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

        return new SeatTable(config);
    }

    public IReadOnlyList<SeatDefinition> SeatsFor(string name)
    {
        return _config.GetValueOrDefault(name, EmptySeats);
    }
}