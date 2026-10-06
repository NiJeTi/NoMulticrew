using System.Diagnostics.CodeAnalysis;
using System.Text;
using HarmonyLib;
using NoMulticrew.Seats;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMulticrew.Patches;

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

    public static bool Lists(ClientSession client, Aircraft aircraft, int seat, int station)
    {
        return Plugin.SeatTable.IsShared(aircraft) || client.Crew.Holder(aircraft, station) == seat;
    }

    public static string Key(ClientSession client, Aircraft aircraft, int seat)
    {
        var key = new StringBuilder().Append(seat).Append(':');

        foreach (var station in aircraft.weaponStations)
        {
            key.Append(
                !Lists(client, aircraft, seat, station.Number) ? '-'
                : client.Crew.CanSelect(aircraft, seat, station.Number) ? 'o'
                : 'x'
            );
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

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(RadialMenuMain), "SetupWeapons")]
internal static class RadialMenuMain_SetupWeapons
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        return WeaponWheel.Present();
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(
        Aircraft ___aircraft,
        RadialMenuAction[] ___actionsWeapons,
        List<RadialMenuAction> ___allowedActionsWeapons,
        RadialMenuAction ___actionWeaponPrefab,
        GameObject ___actionPrefab,
        GameObject ___actionsWeaponsContainer,
        GameObject ___sectorObject,
        List<GameObject> ___actionObjectsWeapons,
        ref bool ___showWeaponWheel,
        ref float ___degreesPerActionWeapons
    )
    {
        var client = Plugin.Client;
        if (client == null)
        {
            WeaponWheel.Greyed.Clear();
            return true;
        }

        var seat = WeaponWheel.SeatOf(client, ___aircraft);

        ___showWeaponWheel = false;

        foreach (var action in ___allowedActionsWeapons)
        {
            if (action.GetActionType() == RadialMenuAction.ActionType.SelectWeapon)
            {
                Object.Destroy(action);
            }
        }

        ___allowedActionsWeapons.Clear();
        WeaponWheel.Greyed.Clear();

        foreach (var actionObject in ___actionObjectsWeapons)
        {
            Object.Destroy(actionObject);
        }

        ___actionObjectsWeapons.Clear();

        if (seat == SeatTable.Pilot)
        {
            foreach (var action in ___actionsWeapons)
            {
                if (action.AllowedOnAircraft(___aircraft))
                {
                    ___allowedActionsWeapons.Add(action);
                    ___showWeaponWheel = true;
                }
            }
        }

        foreach (var station in ___aircraft.weaponStations)
        {
            if (!WeaponWheel.Lists(client, ___aircraft, seat, station.Number))
            {
                continue;
            }

            var action = Object.Instantiate(___actionWeaponPrefab);
            action.SetWeapon(station.WeaponInfo, station.Number);
            ___allowedActionsWeapons.Add(action);

            if (!client.Crew.CanSelect(___aircraft, seat, station.Number))
            {
                WeaponWheel.Greyed.Add(action);
            }
        }

        if (___allowedActionsWeapons.Count > 2)
        {
            ___showWeaponWheel = true;
        }

        ___degreesPerActionWeapons = 360f / ___allowedActionsWeapons.Count;
        var degrees = ___degreesPerActionWeapons;

        for (var j = 0; j < ___allowedActionsWeapons.Count; j++)
        {
            var sector = Object.Instantiate(___sectorObject, ___actionsWeaponsContainer.transform);
            var background = sector.GetComponent<Image>();
            background.fillAmount = degrees / 360f;
            sector.transform.localEulerAngles = new Vector3(0f, 0f, (0f - (j - 0.5f)) * degrees);

            var icon = Object.Instantiate(___actionPrefab, ___actionsWeaponsContainer.transform);
            var iconImage = icon.GetComponent<Image>();
            var ammoText = icon.transform.Find("Text").GetComponent<Text>();

            var action = ___allowedActionsWeapons[j];
            action.Setup(background, iconImage, ammoText);

            if (WeaponWheel.Greyed.Contains(action))
            {
                WeaponWheel.Grey(action);
            }

            icon.transform.localPosition = 90f * new Vector3(
                Mathf.Sin(j * degrees * (MathF.PI / 180f)),
                Mathf.Cos(j * degrees * (MathF.PI / 180f)),
                0f
            );

            ___actionObjectsWeapons.Add(icon);
            ___actionObjectsWeapons.Add(sector);
        }

        return false;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(RadialMenuMain), nameof(RadialMenuMain.RefreshWeapons))]
internal static class RadialMenuMain_RefreshWeapons
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        return WeaponWheel.Present();
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix()
    {
        foreach (var action in WeaponWheel.Greyed)
        {
            WeaponWheel.Grey(action);
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(RadialMenuMain), nameof(RadialMenuMain.OpenMenu))]
internal static class RadialMenuMain_OpenMenu
{
    private static readonly Action<RadialMenuMain>? SetupWeapons =
        AccessTools.Method(typeof(RadialMenuMain), "SetupWeapons") is { } method
            ? AccessTools.MethodDelegate<Action<RadialMenuMain>>(method)
            : null;

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        return WeaponWheel.Present();
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(
        RadialMenuMain __instance,
        ref Aircraft? ___aircraft,
        RadialMenuMain.RadialMenuType ___currentState,
        ref float ___lastOpen,
        ref Vector3 ___mousePos
    )
    {
        var client = Plugin.Client;
        if (client == null)
        {
            return true;
        }

        var crewed = client.BackSeat.Aircraft;
        if (crewed != null)
        {
            if (___currentState == RadialMenuMain.RadialMenuType.Main)
            {
                __instance.CloseMenu();
                return false;
            }

            var crewKey = WeaponWheel.Key(client, crewed, client.BackSeat.SeatIndex);
            if (crewKey != WeaponWheel.Built || !ReferenceEquals(___aircraft, crewed))
            {
                ___aircraft = crewed;
                WeaponWheel.Built = crewKey;
                SetupWeapons!(__instance);
            }

            __instance.RefreshWeapons();
            ___lastOpen = Time.realtimeSinceStartup;
            ___mousePos = Input.mousePosition;

            return false;
        }

        if (!GameManager.GetLocalAircraft(out var flown))
        {
            ___aircraft = null;
            WeaponWheel.Built = null;
            return true;
        }

        var key = WeaponWheel.Key(client, flown, SeatTable.Pilot);
        if (key != WeaponWheel.Built)
        {
            ___aircraft = null;
            WeaponWheel.Built = key;
        }

        return true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(RadialMenuAction), nameof(RadialMenuAction.TriggerAction))]
internal static class RadialMenuAction_TriggerAction
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(RadialMenuAction __instance, Aircraft aircraft)
    {
        var client = Plugin.Client;
        if (client == null || __instance.GetActionType() != RadialMenuAction.ActionType.SelectWeapon)
        {
            return true;
        }

        var station = __instance.weapon_number;
        var backSeat = client.BackSeat;

        if (ReferenceEquals(backSeat.Aircraft, aircraft))
        {
            if (!client.Crew.CanSelect(aircraft, backSeat.SeatIndex, station))
            {
                return client.Crew.RefuseStation(aircraft, station);
            }

            backSeat.Select(station);
            return false;
        }

        if (!GameManager.IsLocalAircraft(aircraft))
        {
            return false;
        }

        return !client.Crew.BlocksStation(aircraft, station) || client.Crew.RefuseStation(aircraft, station);
    }
}
