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

        return crew.Refuse("Back seat has this station");
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.SetFiringState))]
internal static class Unit_SetFiringState
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Unit __instance, int index, bool firing)
    {
        var crew = Plugin.Client?.Crew;
        if (!firing || crew == null || !crew.BlocksStation(__instance, index))
        {
            return true;
        }

        return crew.Refuse("Back seat has this station");
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

        return crew.Refuse("Back seat has this station");
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), nameof(Aircraft.CmdToggleRadar))]
internal static class Aircraft_CmdToggleRadar
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance)
    {
        var crew = Plugin.Client?.Crew;
        if (crew == null || !crew.BlocksSensors(__instance))
        {
            return true;
        }

        return crew.Refuse("Back seat runs the sensors");
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

        return !client.Crew.BlocksSensors(aircraft) && !ReferenceEquals(client.BackSeat.Aircraft, aircraft);
    }
}
