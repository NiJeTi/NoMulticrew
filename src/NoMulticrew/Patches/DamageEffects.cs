using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(DamageEffects), nameof(DamageEffects.ArmorPenetrate))]
internal static class DamageEffects_ArmorPenetrate
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(PersistentID dealerID, out bool __state)
    {
        __state = Plugin.Server?.Economy.EnterGunContext(dealerID) == true;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Finalizer(bool __state)
    {
        if (__state)
        {
            Plugin.Server!.Economy.ExitContext();
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(DamageEffects), nameof(DamageEffects.BlastFrag))]
internal static class DamageEffects_BlastFrag
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(PersistentID missileID, out bool __state)
    {
        UnitRegistry.TryGetUnit<Missile>(missileID, out var missile);

        __state = Plugin.Server?.Economy.EnterMissileScope(missile) == true;
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
