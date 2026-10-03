using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

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