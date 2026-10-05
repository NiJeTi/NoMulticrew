using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using NoMulticrew.Networking;
using NuclearOption.Networking;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.LaunchMount))]
internal static class WeaponStation_LaunchMount
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(WeaponStation __instance, Unit owner)
    {
        Plugin.Server?.Economy.OnLaunch(owner, __instance);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class Unit_UserCode_CmdClaimHit
{
    private static readonly MethodBase? Target = UserCode.Find(typeof(Unit), "CmdClaimHit");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null)
        {
            Plugin.Logger.LogError(
                "Unit.UserCode_CmdClaimHit not found: gun damage from a crewed aircraft is credited to the pilot"
            );
        }

        return Target != null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodBase TargetMethod()
    {
        return Target!;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Unit __instance)
    {
        Plugin.Server?.Economy.OnClaim(__instance);
    }
}

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

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.RecordDamage))]
internal static class Unit_RecordDamage
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Unit __instance, PersistentID lastDamagedBy, float damageAmount)
    {
        Plugin.Server?.Economy.OnDamage(__instance, lastDamagedBy, damageAmount);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(FactionHQ), nameof(FactionHQ.RewardPlayer))]
internal static class FactionHQ_RewardPlayer
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(
        FactionHQ __instance,
        Player player,
        Unit target,
        float rewardAllocation,
        float rewardScore,
        FactionHQ.RewardType missionType
    )
    {
        var economy = Plugin.Server?.Economy;

        return economy == null
            || !economy.Reward(__instance, player, target, rewardAllocation, rewardScore, missionType);
    }
}
