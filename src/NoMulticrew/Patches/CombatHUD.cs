using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DisplayHit))]
internal static class CombatHUD_DisplayHit
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix()
    {
        if (!Unit_RegisterHit.HidesNextDisplay)
        {
            return true;
        }

        Unit_RegisterHit.HidesNextDisplay = false;

        return false;
    }
}
