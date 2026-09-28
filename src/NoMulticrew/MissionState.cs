using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew;

internal sealed class MissionState : IDisposable
{
    public SeatTable Seats { get; }

    private MissionState(SeatTable seats)
    {
        Seats = seats;
    }

    public static MissionState? TryCreate()
    {
        if (!MissionTracker.InMission)
        {
            return null;
        }

        var table = SeatTableValidator.Validate(SeatTable.Load());

        table.WarnUnmatchedKeys(Resources.FindObjectsOfTypeAll<AircraftDefinition>());

        return new MissionState(table);
    }

    public void Dispose()
    {
    }
}
