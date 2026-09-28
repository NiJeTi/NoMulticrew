using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatTable(Dictionary<string, IReadOnlyList<SeatDefinition>> byJsonKey)
{
    private static readonly IReadOnlyList<SeatDefinition> None = [];

    private readonly Dictionary<string, IReadOnlyList<SeatDefinition>> _byJsonKey = byJsonKey;

    public IReadOnlyCollection<string> Keys => _byJsonKey.Keys;

    public static SeatTable Defaults()
    {
        return new SeatTable(new Dictionary<string, IReadOnlyList<SeatDefinition>>
        {
            ["CI-22"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0f, -0.1f, -1.4f))],
            ["EW-25"] = [new SeatDefinition(SeatRole.Wso, new Vector3(0f, -0.1f, -1.6f))],
            ["SAH-46"] = [new SeatDefinition(SeatRole.Gunner, new Vector3(0f, -0.1f, -1.5f))],
            ["VL-49"] = [new SeatDefinition(SeatRole.Gunner, new Vector3(-1.1f, -0.3f, -2.0f))],
        });
    }

    public static SeatTable Load()
    {
        var overrides = SeatTableFile.TryRead();

        if (overrides == null)
        {
            Plugin.Logger.LogInfo($"Seat table: shipped defaults, {Defaults().Keys.Count} airframes");

            return Defaults();
        }

        Plugin.Logger.LogInfo(
            $"Seat table: {SeatTableFile.Path} overrides in effect, {overrides.Keys.Count} airframes"
        );

        return overrides;
    }

    public IReadOnlyList<SeatDefinition> SeatsFor(string jsonKey)
    {
        return _byJsonKey.TryGetValue(jsonKey, out var seats) ? seats : None;
    }

    public IReadOnlyList<SeatDefinition> SeatsFor(UnitDefinition? definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.jsonKey))
        {
            return None;
        }

        return SeatsFor(definition.jsonKey);
    }

    public bool HasCrewSeats(UnitDefinition? definition)
    {
        return SeatsFor(definition).Count > 0;
    }

    public SeatTable Without(string jsonKey)
    {
        var copy = new Dictionary<string, IReadOnlyList<SeatDefinition>>(_byJsonKey);

        copy.Remove(jsonKey);

        return new SeatTable(copy);
    }

    public void WarnUnmatchedKeys(IEnumerable<UnitDefinition> definitions)
    {
        var known = new HashSet<string>(
            definitions.Where(definition => definition != null && !string.IsNullOrEmpty(definition.jsonKey))
                .Select(definition => definition.jsonKey)
        );

        foreach (var jsonKey in _byJsonKey.Keys.Where(jsonKey => !known.Contains(jsonKey)))
        {
            Plugin.Logger.LogWarning(
                $"Seat table: '{jsonKey}' matches no airframe in this build, so it seats nobody. "
                + "Check the key against the airframe definitions."
            );
        }
    }
}
