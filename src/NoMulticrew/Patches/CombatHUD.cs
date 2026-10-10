using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class CombatHUD_DisplayCountermeasures
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(CombatHUD))
            .Where(
                x => x.Name is nameof(CombatHUD.DisplayCountermeasures) or nameof(CombatHUD.DisplayCountermeasureAmmo)
            );
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix()
    {
        return Plugin.Client?.BackSeat.Aircraft == null;
    }
}