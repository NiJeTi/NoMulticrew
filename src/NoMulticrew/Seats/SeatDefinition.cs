using UnityEngine;

namespace NoMulticrew.Seats;

internal sealed class SeatDefinition(SeatRole role, Vector3 viewOffset)
{
    public SeatRole Role { get; } = role;

    public Vector3 ViewOffset { get; } = viewOffset;

    public override string ToString()
    {
        return $"{Role} at {ViewOffset:F2}";
    }
}

[Serializable]
internal sealed class SeatEntry
{
    public string role = nameof(SeatRole.Wso);
    public float offsetX = 0f;
    public float offsetY = 0f;
    public float offsetZ = -1.6f;

    public SeatDefinition ToDefinition()
    {
        var parsed = Enum.TryParse<SeatRole>(role, ignoreCase: true, out var value) ? value : SeatRole.Wso;

        return new SeatDefinition(parsed, new Vector3(offsetX, offsetY, offsetZ));
    }
}

[Serializable]
internal sealed class AirframeEntry
{
    public string jsonKey = "";
    public SeatEntry[] seats = [];
}

[Serializable]
internal sealed class SeatTableDocument
{
    public AirframeEntry[] airframes = [];
}
