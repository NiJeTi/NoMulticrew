using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.Fire))]
internal static class WeaponManager_Fire
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponManager __instance, Aircraft ___aircraft)
    {
        var client = Plugin.Client;
        var station = __instance.currentWeaponStation;

        if (client == null || station == null || !client.PilotSeat.Blocks(___aircraft, station.Number))
        {
            return true;
        }

        return client.Notices.RefuseStation(___aircraft, station.Number);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), "OrganizeWeaponStations")]
internal static class WeaponManager_OrganizeWeaponStations
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Aircraft ___aircraft)
    {
        Plugin.Server?.Crew.LoadoutChanged(___aircraft);
        Plugin.Client?.BackSeat.LoadoutChanged(___aircraft);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.TargetListChanged))]
internal static class WeaponManager_TargetListChanged
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponManager __instance, Aircraft ___aircraft)
    {
        var client = Plugin.Client;
        if (client == null)
        {
            return true;
        }

        if (ReferenceEquals(client.BackSeat.Aircraft, ___aircraft))
        {
            client.BackSeat.Weapons.PushTargets();
            return false;
        }

        var station = __instance.currentWeaponStation;

        return station == null || !client.PilotSeat.Blocks(___aircraft, station.Number);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.NextWeaponStation))]
internal static class WeaponManager_NextWeaponStation
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft ___aircraft)
    {
        return Plugin.Client?.PilotSeat.Cycle(___aircraft, 1) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.PreviousWeaponStation))]
internal static class WeaponManager_PreviousWeaponStation
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft ___aircraft)
    {
        return Plugin.Client?.PilotSeat.Cycle(___aircraft, -1) != true;
    }
}