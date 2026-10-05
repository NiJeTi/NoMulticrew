using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatView
{
    public IReadOnlyCollection<string> HardpointSets { get; }

    public Vector3 Offset { get; }

    public SeatView(Vector3 offset)
        : this([], offset)
    {
    }

    public SeatView(string[] hardpointSets, Vector3 offset)
    {
        HardpointSets = hardpointSets;
        Offset = offset;
    }

    public override string ToString()
    {
        return HardpointSets.Count == 0
            ? $"default at {Offset:F2}"
            : $"[{string.Join(", ", HardpointSets)}] at {Offset:F2}";
    }
}

internal sealed class SeatDefinition
{
    private readonly HashSet<string> _weapons;

    public SeatRole Role { get; }

    public IReadOnlyCollection<string> Weapons => _weapons;

    public IReadOnlyList<SeatView> Views { get; }

    public SeatView DefaultView { get; }

    public bool HasOneDefaultView { get; }

    public SeatDefinition(SeatRole role, string[] weapons, SeatView[] views)
    {
        Role = role;
        _weapons = [.. weapons];
        Views = views;

        var defaults = views.Where(x => x.HardpointSets.Count == 0).ToList();

        HasOneDefaultView = defaults.Count == 1;
        DefaultView = defaults.FirstOrDefault() ?? new SeatView(Vector3.zero);
    }

    public bool Operates(string weaponName)
    {
        return _weapons.Contains(weaponName);
    }

    public SeatView ViewFor(ISet<string> hardpointSets)
    {
        return Views.FirstOrDefault(x => x.HardpointSets.Count > 0 && hardpointSets.SetEquals(x.HardpointSets))
            ?? DefaultView;
    }

    public override string ToString()
    {
        return $"{Role} operating [{string.Join(", ", _weapons)}], views {string.Join("; ", Views)}";
    }
}
