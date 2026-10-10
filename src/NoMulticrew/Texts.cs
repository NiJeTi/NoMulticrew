using NoMulticrew.Networking.Messages;
using NoMulticrew.Seats;

namespace NoMulticrew;

internal static class Texts
{
    public static class Roles
    {
        public const string Pilot = "Pilot";
        public const string Wso = "WSO";

        public static string Label(Role role)
        {
            return role == Role.Pilot ? Pilot : Wso;
        }
    }

    public static class Requests
    {
        public const string Seated = "Seated";
        public const string LeaveAircraftFirst = "You need to leave your aircraft first";

        public static string Incoming(string joiner)
        {
            return $"{joiner} wants to join as {Roles.Wso}";
        }

        public static string Joined(string joiner)
        {
            return $"{joiner} joined as {Roles.Wso}";
        }
    }

    public static class Crew
    {
        public const string BailOutWarning = "Press Eject again to bail out";

        public static string CrewmateLeft(string crewmate)
        {
            return $"{crewmate} left {Roles.Wso} seat";
        }
    }

    public static class Weapons
    {
        public const string NoneForSeat = "No weapons for this seat";

        public static string HeldBy(Role holder)
        {
            return $"{Roles.Label(holder)} has this weapon";
        }

        public static string InUseBy(Role holder)
        {
            return $"{Roles.Label(holder)} is using this weapon";
        }

        public static string ExclusiveTo(Role holder)
        {
            return $"{Roles.Label(holder)}-only weapon";
        }
    }

    public static class CrewScreen
    {
        public const string ShortName = "CRW";
        public const string Title = "CREW";

        public const string CrewTitle = "CREW";
        public const string StationsTitle = "STATIONS";
        public const string ActionsTitle = "ACTIONS";
        public const string SeatsTitle = "SEATS";

        public const string NotInAircraft = "NOT IN AN AIRCRAFT";
        public const string None = "NONE";
        public const string NoCrew = "NO CREW";
        public const string NoSeats = "NO SEATS AVAILABLE";

        public const string Accept = "ACCEPT";
        public const string Decline = "DECLINE";
        public const string Requests = "ACCEPT REQUESTS";
        public const string Leave = "LEAVE";
        public const string ConfirmLeave = "CONFIRM LEAVE";
        public const string Request = "REQUEST";
        public const string Waiting = "WAITING";

        public const string Declined = "PILOT DECLINED";
        public const string NoAnswer = "NO ANSWER";
        public const string PilotLeft = "PILOT LEFT";
        public const string AircraftLost = "AIRCRAFT LOST";
        public const string NoPilot = "NO PILOT";
        public const string NotAtAirbase = "NOT AT AN AIRBASE";
        public const string NotTakingCrew = "NOT TAKING CREW";
        public const string SeatTaken = "SEAT TAKEN";
        public const string LeaveAircraft = "LEAVE YOUR AIRCRAFT";

        public static string Outcome(CrewJoinOutcome outcome)
        {
            return outcome switch
            {
                CrewJoinOutcome.Declined => Declined,
                CrewJoinOutcome.NoAnswer => NoAnswer,
                CrewJoinOutcome.PilotLeft => PilotLeft,
                CrewJoinOutcome.AircraftLost => AircraftLost,
                CrewJoinOutcome.NoPilot => NoPilot,
                CrewJoinOutcome.NotAtAirbase => NotAtAirbase,
                CrewJoinOutcome.NotTakingCrew => NotTakingCrew,
                CrewJoinOutcome.SeatTaken => SeatTaken,
                CrewJoinOutcome.LeaveAircraft => LeaveAircraft,
                _ => "",
            };
        }

        public static string RoleLabel(Role role)
        {
            return Roles.Label(role).ToUpperInvariant();
        }

        public static string Pilot(string name)
        {
            return $"PILOT  {name}";
        }

        public static string Wso(string name, bool here)
        {
            return $"{RoleLabel(Role.Wso)}  {name}{(here ? " <" : "")}";
        }

        public static string Pending(float amount)
        {
            return $"PENDING  +{amount:F0}";
        }

        public static string Incoming(string joiner)
        {
            return $"{joiner} WANTS {Roles.Wso}";
        }

        public static string Offer(string unit, string pilot)
        {
            return $"{unit}  ·  {pilot}  {Roles.Wso}";
        }

        public static string Station(int index, string weapon, Role? holder, bool selected)
        {
            var label = holder is { } role ? RoleLabel(role) : "";

            return $"{index}  {weapon}  {label}{(selected ? " >" : "")}";
        }
    }

}
