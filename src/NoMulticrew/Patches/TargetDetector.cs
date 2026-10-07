using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(TargetDetector), "TargetSearch")]
internal static class TargetDetector_TargetSearch
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(TargetDetector __instance)
    {
        return Plugin.Client?.BackSeat.Searches(__instance) ?? true;
    }
}
