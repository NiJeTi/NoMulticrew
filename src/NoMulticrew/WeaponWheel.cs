using System.Text;
using HarmonyLib;
using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew;

internal static class WeaponWheel
{
    private static readonly AccessTools.FieldRef<RadialMenuAction, Color> DefaultColorRef =
        AccessTools.FieldRefAccess<RadialMenuAction, Color>("defaultColor");

    private static readonly AccessTools.FieldRef<RadialMenuAction, Color> SelectedColorRef =
        AccessTools.FieldRefAccess<RadialMenuAction, Color>("selectedColor");

    public static HashSet<RadialMenuAction> Greyed { get; } = [];

    public static string? Built { get; set; }

    public static int SeatOf(ClientSession client, Aircraft aircraft)
    {
        return ReferenceEquals(client.BackSeat.Aircraft, aircraft) ? client.BackSeat.SeatIndex : SeatTable.Pilot;
    }

    public static string Key(ClientSession client, Aircraft aircraft, int seat)
    {
        var key = new StringBuilder().Append(seat).Append(':');

        foreach (var station in aircraft.weaponStations)
        {
            key.Append(client.Crew.CanSelect(aircraft, seat, station.Number) ? 'o' : 'x');
        }

        return key.ToString();
    }

    public static void Grey(RadialMenuAction action)
    {
        DefaultColorRef(action) = Color.gray;
        SelectedColorRef(action) = Color.gray;
        action.UnHover();
    }
}
