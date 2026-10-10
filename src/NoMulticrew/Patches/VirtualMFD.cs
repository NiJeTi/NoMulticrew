using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(VirtualMFD), "Start")]
internal static class VirtualMFD_Start
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(VirtualMFD __instance)
    {
        Plugin.Client?.AttachMfd(__instance);
    }
}