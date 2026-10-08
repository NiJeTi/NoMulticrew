using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Seats;

namespace NoMulticrew.Patches;

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
            if (!client.Crew.CanSelect(aircraft, Role.Wso, station))
            {
                return client.Crew.RefuseSelection(aircraft, station);
            }

            backSeat.Select(station);
            return false;
        }

        if (!GameManager.IsLocalAircraft(aircraft))
        {
            return false;
        }

        return !client.PilotSeat.Blocks(aircraft, station) || client.Crew.RefuseSelection(aircraft, station);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(RadialMenuAction), nameof(RadialMenuAction.AllowedOnAircraft))]
internal static class RadialMenuAction_AllowedOnAircraft
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(RadialMenuAction __instance, Aircraft aircraft, ref bool __result)
    {
        if (__result
            && __instance.GetActionType() != RadialMenuAction.ActionType.SelectWeapon
            && Plugin.Client?.LocalRole(aircraft) == Role.Wso)
        {
            __result = false;
        }
    }
}
