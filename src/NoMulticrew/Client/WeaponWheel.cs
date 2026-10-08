using System.Text;
using HarmonyLib;
using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew.Client;

internal sealed class WeaponWheel
{
    private static readonly AccessTools.FieldRef<RadialMenuAction, Color> DefaultColorRef =
        AccessTools.FieldRefAccess<RadialMenuAction, Color>("defaultColor");

    private static readonly AccessTools.FieldRef<RadialMenuAction, Color> SelectedColorRef =
        AccessTools.FieldRefAccess<RadialMenuAction, Color>("selectedColor");

    public HashSet<RadialMenuAction> Greyed { get; } = [];

    public string? Built { get; set; }

    public string Key(ClientSession client, Aircraft aircraft, Role role)
    {
        var key = new StringBuilder().Append(role).Append(':');

        foreach (var station in aircraft.weaponStations)
        {
            key.Append(client.Crew.CanSelect(aircraft, role, station.Number) ? 'o' : 'x');
        }

        return key.ToString();
    }

    public void Clear()
    {
        Greyed.Clear();
        Built = null;
    }

    public static void Grey(RadialMenuAction action)
    {
        DefaultColorRef(action) = Color.gray;
        SelectedColorRef(action) = Color.gray;
        action.UnHover();
    }
}
