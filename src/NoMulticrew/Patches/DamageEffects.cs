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
        __state = Plugin.Server?.Economy.EnterGun(dealerID) == true;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Finalizer(bool __state)
    {
        if (__state)
        {
            Plugin.Server!.Economy.Exit();
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
        var server = Plugin.Server;
        if (server == null)
        {
            __state = false;
            return;
        }

        UnitRegistry.TryGetUnit<Missile>(missileID, out var missile);

        __state = server.Economy.EnterMissile(missile);
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Finalizer(bool __state)
    {
        if (__state)
        {
            Plugin.Server!.Economy.Exit();
        }
    }
}