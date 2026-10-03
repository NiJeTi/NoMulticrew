using NoMulticrew.Seats;
using NuclearOption.Networking;

namespace NoMulticrew.Crew;

internal sealed class CrewSeat
{
    public SeatRole Role { get; }

    public Player? Occupant { get; set; }

    public CrewSeat(SeatRole role)
    {
        Role = role;
    }
}
