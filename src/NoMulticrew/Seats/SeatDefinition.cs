using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatDefinition
{
    public SeatRole Role { get; }

    public Vector3 ViewOffset { get; }

    public SeatDefinition(SeatRole role, Vector3 viewOffset)
    {
        Role = role;
        ViewOffset = viewOffset;
    }

    public override string ToString()
    {
        return $"{Role} at {ViewOffset:F2}";
    }
}