using UnityEngine;

namespace NoMulticrew.Seats;

internal readonly record struct SeatState(bool WsoAboard, int WsoStation, int PilotStation);

internal sealed record ScreenPlacement(Vector3 Centre, Vector3 Normal, Vector2 Size, Rect Uv);

internal sealed record PanelPlacement(
    Vector3 TopLeft,
    Vector3 TopRight,
    Vector3 BottomRight,
    Vector3 BottomLeft,
    Vector2 UvTopLeft,
    Vector2 UvBottomRight,
    Vector3 Normal
);

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

    public bool IsShared { get; }

    public IReadOnlyCollection<string> Weapons => _weapons;

    public IReadOnlyList<SeatView> Views { get; }

    public SeatView DefaultView { get; }

    public bool HasOneDefaultView { get; }

    public ScreenPlacement? Screen { get; }

    public PanelPlacement? Panel { get; }

    public SeatDefinition(
        string[] weapons,
        SeatView[] views,
        ScreenPlacement? screen = null,
        PanelPlacement? panel = null
    )
        : this(false, weapons, views, screen, panel)
    {
    }

    private SeatDefinition(
        bool shared,
        string[] weapons,
        SeatView[] views,
        ScreenPlacement? screen,
        PanelPlacement? panel
    )
    {
        IsShared = shared;
        _weapons = [.. weapons];
        Views = views;
        Screen = screen;
        Panel = panel;

        var defaults = views.Where(x => x.HardpointSets.Count == 0).ToList();

        HasOneDefaultView = defaults.Count == 1;
        DefaultView = defaults.FirstOrDefault() ?? new SeatView(Vector3.zero);
    }

    public static SeatDefinition Shared(SeatView[] views, PanelPlacement? panel = null)
    {
        return new SeatDefinition(true, [], views, null, panel);
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
        var weapons = IsShared ? "sharing every station" : $"operating [{string.Join(", ", _weapons)}]";

        return $"WSO {weapons}, views {string.Join("; ", Views)}";
    }
}
