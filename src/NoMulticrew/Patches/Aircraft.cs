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
