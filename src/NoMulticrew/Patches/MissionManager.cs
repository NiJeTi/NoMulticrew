using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(MissionManager), nameof(MissionManager.SetMission))]
internal static class MissionManager_SetMission
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix()
    {
        Plugin.Server?.Clear();
        Plugin.Client?.Crew.Clear();
    }
}
