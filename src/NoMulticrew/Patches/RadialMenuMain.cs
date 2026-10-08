using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Client;
using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(RadialMenuMain), "SetupWeapons")]
internal static class RadialMenuMain_SetupWeapons
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Aircraft ___aircraft, List<RadialMenuAction> ___allowedActionsWeapons)
    {
        var client = Plugin.Client;
        if (client == null)
        {
            return;
        }

        var wheel = client.Wheel;
        var role = client.LocalRole(___aircraft) ?? Role.Pilot;

        wheel.Greyed.Clear();

        foreach (var action in ___allowedActionsWeapons)
        {
            if (action.GetActionType() == RadialMenuAction.ActionType.SelectWeapon
                && !client.Crew.CanSelect(___aircraft, role, action.weapon_number))
            {
                wheel.Greyed.Add(action);
                WeaponWheel.Grey(action);
            }
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(RadialMenuMain), nameof(RadialMenuMain.RefreshWeapons))]
internal static class RadialMenuMain_RefreshWeapons
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix()
    {
        var wheel = Plugin.Client?.Wheel;
        if (wheel == null)
        {
            return;
        }

        foreach (var action in wheel.Greyed)
        {
            WeaponWheel.Grey(action);
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(RadialMenuMain), nameof(RadialMenuMain.OpenMenu))]
internal static class RadialMenuMain_OpenMenu
{
    private static readonly Action<RadialMenuMain> SetupWeapons = AccessTools.MethodDelegate<Action<RadialMenuMain>>(
        GameMembers.Method(typeof(RadialMenuMain), "SetupWeapons")
    );

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

        var wheel = client.Wheel;
        var crewed = client.BackSeat.Aircraft;
        if (crewed != null)
        {
            if (___currentState == RadialMenuMain.RadialMenuType.Main)
            {
                __instance.CloseMenu();
                return false;
            }

            var crewKey = wheel.Key(client, crewed, Role.Wso);
            if (crewKey != wheel.Built || !ReferenceEquals(___aircraft, crewed))
            {
                ___aircraft = crewed;
                wheel.Built = crewKey;
                SetupWeapons(__instance);
            }

            __instance.RefreshWeapons();
            ___lastOpen = Time.realtimeSinceStartup;
            ___mousePos = Input.mousePosition;

            return false;
        }

        if (!GameManager.GetLocalAircraft(out var flown))
        {
            ___aircraft = null;
            wheel.Built = null;
            return true;
        }

        var key = wheel.Key(client, flown, Role.Pilot);
        if (key != wheel.Built)
        {
            ___aircraft = null;
            wheel.Built = key;
        }

        return true;
    }
}
