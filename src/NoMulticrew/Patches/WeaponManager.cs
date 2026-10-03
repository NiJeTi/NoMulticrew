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

        return crew.Refuse("Back seat has this station");
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
        return Plugin.Client?.Crew.BlocksSensors(AircraftRef(__instance)) != true;
    }
}
