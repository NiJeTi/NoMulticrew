using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class Spawner_SpawnMissile
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(Spawner)).Where(x => x.Name == nameof(Spawner.SpawnMissile));
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Missile __result, Unit owner)
    {
        Plugin.Server?.Economy.OnSpawn(__result, owner);
    }
}