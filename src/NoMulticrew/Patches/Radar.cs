using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Radar), "TargetSearch")]
internal static class Radar_TargetSearch
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Radar __instance)
    {
        return TargetDetector_TargetSearch.Searches(__instance);
    }
}
