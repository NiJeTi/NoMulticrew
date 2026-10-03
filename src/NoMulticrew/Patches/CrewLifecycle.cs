using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.Networkdisabled), MethodType.Setter)]
internal static class Unit_SetNetworkdisabled
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Unit __instance, bool value)
    {
        if (value)
        {
            Plugin.Server?.Crew.Dissolve(__instance.persistentID);
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), nameof(Aircraft.StartEjectionSequence))]
internal static class Aircraft_StartEjectionSequence
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Aircraft __instance)
    {
        Plugin.Server?.Crew.Dissolve(__instance.persistentID);
    }
}
