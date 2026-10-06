using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Missile), "PenetrateObject")]
internal static class Missile_PenetrateObject
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Missile __instance, out bool __state)
    {
        __state = Plugin.Server?.Economy.EnterMissileScope(__instance) == true;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Finalizer(bool __state)
    {
        if (__state)
        {
            Plugin.Server!.Economy.ExitMissileScope();
        }
    }
}
