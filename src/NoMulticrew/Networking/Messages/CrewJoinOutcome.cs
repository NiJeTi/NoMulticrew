namespace NoMulticrew.Networking.Messages;

internal enum CrewJoinOutcome : byte
{
    Seated = 0,
    Declined = 1,
    NoAnswer = 2,
    PilotLeft = 3,
    AircraftLost = 4,
    NoPilot = 5,
    NotAtAirbase = 6,
    NotTakingCrew = 7,
    SeatTaken = 8,
    LeaveAircraft = 9,
}