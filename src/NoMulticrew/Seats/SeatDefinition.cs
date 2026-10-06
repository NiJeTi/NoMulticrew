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

internal sealed class SeatDefinition
{
    private readonly HashSet<string> _weapons;

    public bool IsShared { get; }

    public IReadOnlyCollection<string> Weapons => _weapons;

    public Vector3 View { get; }

    public ScreenPlacement? Screen { get; }

    public PanelPlacement? Panel { get; }

    public SeatDefinition(
        string[] weapons,
        Vector3 view,
        ScreenPlacement? screen = null,
        PanelPlacement? panel = null
    )
        : this(false, weapons, view, screen, panel)
    {
    }

    private SeatDefinition(
        bool shared,
        string[] weapons,
        Vector3 view,
        ScreenPlacement? screen,
        PanelPlacement? panel
    )
    {
        IsShared = shared;
        _weapons = [.. weapons];
        View = view;
        Screen = screen;
        Panel = panel;
    }

    public static SeatDefinition Shared(Vector3 view, PanelPlacement? panel = null)
    {
        return new SeatDefinition(true, [], view, null, panel);
    }

    public bool Operates(string weaponName)
    {
        return _weapons.Contains(weaponName);
    }

    public override string ToString()
    {
        var weapons = IsShared ? "sharing every station" : $"operating [{string.Join(", ", _weapons)}]";

        return $"WSO {weapons}, view at {View:F2}";
    }
}
