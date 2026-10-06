using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), nameof(Aircraft.SetActiveStation))]
internal static class Aircraft_SetActiveStation
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance, byte stationIndex)
    {
        var crew = Plugin.Client?.Crew;
        if (crew == null || !crew.BlocksStation(__instance, stationIndex))
        {
            return true;
        }

        return crew.RefuseStation(__instance, stationIndex);
    }
}

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
[HarmonyPatch(typeof(Aircraft), "RpcSetActiveStation")]
internal static class Aircraft_RpcSetActiveStation
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Aircraft __instance, byte stationIndex)
    {
        Plugin.Server?.Crew.RecordPilotStation(__instance, stationIndex);
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
