using System.Text;
using HarmonyLib;
using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew;

internal static class WeaponWheel
{
    private static readonly string[] MenuFields =
    [
        "aircraft",
        "actionsWeapons",
        "allowedActionsWeapons",
        "actionWeaponPrefab",
        "actionPrefab",
        "actionsWeaponsContainer",
        "sectorObject",
        "actionObjectsWeapons",
        "showWeaponWheel",
        "degreesPerActionWeapons",
        "currentState",
        "lastOpen",
        "mousePos",
    ];

    private static readonly AccessTools.FieldRef<RadialMenuAction, Color>? DefaultColorRef = ColorRef("defaultColor");

    private static readonly AccessTools.FieldRef<RadialMenuAction, Color>? SelectedColorRef = ColorRef("selectedColor");

    private static bool? _present;

    public static HashSet<RadialMenuAction> Greyed { get; } = [];

    public static string? Built { get; set; }

    public static bool Present()
    {
        if (_present is { } known)
        {
            return known;
        }

        var missing = MenuFields
            .Where(x => AccessTools.Field(typeof(RadialMenuMain), x) == null)
            .Select(x => $"RadialMenuMain.{x}")
            .ToList();

        if (AccessTools.Method(typeof(RadialMenuMain), "SetupWeapons") == null)
        {
            missing.Add("RadialMenuMain.SetupWeapons");
        }

        if (DefaultColorRef == null)
        {
            missing.Add("RadialMenuAction.defaultColor");
        }

        if (SelectedColorRef == null)
        {
            missing.Add("RadialMenuAction.selectedColor");
        }

        if (missing.Count > 0)
        {
            Plugin.Logger.LogError(
                $"Weapon wheel members not found ({string.Join(", ", missing)}): the weapon wheel stays vanilla"
            );
        }

        _present = missing.Count == 0;

        return _present.Value;
    }

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
        DefaultColorRef!(action) = Color.gray;
        SelectedColorRef!(action) = Color.gray;
        action.UnHover();
    }

    private static AccessTools.FieldRef<RadialMenuAction, Color>? ColorRef(string name)
    {
        return AccessTools.Field(typeof(RadialMenuAction), name) is { } field
            ? AccessTools.FieldRefAccess<RadialMenuAction, Color>(field)
            : null;
    }
}
