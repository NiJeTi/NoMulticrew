using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.Fire))]
internal static class WeaponManager_Fire
{
    private static readonly AccessTools.FieldRef<WeaponManager, Aircraft> AircraftRef =
        AccessTools.FieldRefAccess<WeaponManager, Aircraft>("aircraft");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponManager __instance)
    {
        var crew = Plugin.Client?.Crew;
        var station = __instance.currentWeaponStation;

        if (crew == null || station == null || !crew.BlocksStation(AircraftRef(__instance), station.Number))
        {
            return true;
        }

        return crew.RefuseStation(AircraftRef(__instance), station.Number);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), "OrganizeWeaponStations")]
internal static class WeaponManager_OrganizeWeaponStations
{
    private static readonly AccessTools.FieldRef<WeaponManager, Aircraft> AircraftRef =
        AccessTools.FieldRefAccess<WeaponManager, Aircraft>("aircraft");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(WeaponManager __instance)
    {
        var aircraft = AircraftRef(__instance);

        Plugin.Server?.Crew.LoadoutChanged(aircraft);
        Plugin.Client?.BackSeat.LoadoutChanged(aircraft);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.TargetListChanged))]
internal static class WeaponManager_TargetListChanged
{
    private static readonly AccessTools.FieldRef<WeaponManager, Aircraft> AircraftRef =
        AccessTools.FieldRefAccess<WeaponManager, Aircraft>("aircraft");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponManager __instance)
    {
        var client = Plugin.Client;
        if (client == null)
        {
            return true;
        }

        var aircraft = AircraftRef(__instance);

        if (ReferenceEquals(client.BackSeat.Aircraft, aircraft))
        {
            client.BackSeat.Weapons.PushTargets();
            return false;
        }

        var station = __instance.currentWeaponStation;

        return station == null || !client.Crew.BlocksStation(aircraft, station.Number);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.NextWeaponStation))]
internal static class WeaponManager_NextWeaponStation
{
    private static readonly AccessTools.FieldRef<WeaponManager, Aircraft> AircraftRef =
        AccessTools.FieldRefAccess<WeaponManager, Aircraft>("aircraft");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponManager __instance)
    {
        return Plugin.Client?.PilotSeat.Cycle(AircraftRef(__instance), 1) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.PreviousWeaponStation))]
internal static class WeaponManager_PreviousWeaponStation
{
    private static readonly AccessTools.FieldRef<WeaponManager, Aircraft> AircraftRef =
        AccessTools.FieldRefAccess<WeaponManager, Aircraft>("aircraft");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponManager __instance)
    {
        return Plugin.Client?.PilotSeat.Cycle(AircraftRef(__instance), -1) != true;
    }
}
