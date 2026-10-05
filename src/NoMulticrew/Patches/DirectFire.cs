using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using NoMulticrew.Networking;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.NetworkremoteWeaponStates), MethodType.Setter)]
internal static class Unit_SetNetworkremoteWeaponStates
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Unit __instance, ref WeaponMask value)
    {
        Plugin.Server?.Commands.MergeFiring(__instance, ref value);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.SetFiringState))]
internal static class Unit_SetFiringState
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Unit __instance, int index, bool firing)
    {
        var client = Plugin.Client;
        if (client == null)
        {
            return true;
        }

        if (client.BackSeat.Owns(__instance, index))
        {
            client.BackSeat.Weapons.SetFiring(index, firing);
            return false;
        }

        if (!firing || !client.Crew.BlocksStation(__instance, index))
        {
            return true;
        }

        return client.Crew.Refuse("Back seat has this station");
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.RegisterHit))]
internal static class Unit_RegisterHit
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(
        Unit __instance,
        Unit hitUnit,
        Vector3 relativePos,
        Vector3 bulletVelocity,
        WeaponInfo weaponInfo,
        out bool __state
    )
    {
        __state = false;

        var client = Plugin.Client;
        if (client != null)
        {
            var station = __instance.weaponStations.FindIndex(x => x.WeaponInfo == weaponInfo);

            if (client.Crew.BlocksStation(__instance, station))
            {
                return false;
            }

            if (!__instance.IsServer && client.BackSeat.Owns(__instance, station))
            {
                client.BackSeat.Weapons.ClaimHit(hitUnit, relativePos, bulletVelocity, (byte)station);
                return false;
            }
        }

        __state = __instance.IsServer && Plugin.Server?.Economy.EnterHitContext(__instance, weaponInfo) == true;

        return true;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Finalizer(bool __state)
    {
        if (__state)
        {
            Plugin.Server?.Economy.ExitContext();
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(BulletSim), nameof(BulletSim.AddBullet))]
internal static class BulletSim_AddBullet
{
    private static readonly AccessTools.FieldRef<Weapon, WeaponStation?> WeaponStationRef =
        AccessTools.FieldRefAccess<Weapon, WeaponStation?>("weaponStation");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Unit ___owner, Gun ___gun, ref bool ___visualOnly)
    {
        var client = Plugin.Client;
        if (client == null || ___owner == null || ___gun == null)
        {
            return;
        }

        if (___gun.ForceServerAuthority)
        {
            ___visualOnly = !___owner.IsServer;
            return;
        }

        var station = WeaponStationRef(___gun);

        ___visualOnly = ___owner.remoteSim && (station == null || !client.BackSeat.Owns(___owner, station.Number));
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Gun), "SpawnBullet")]
internal static class Gun_SpawnBullet
{
    private static readonly AccessTools.FieldRef<Weapon, WeaponStation?> WeaponStationRef =
        AccessTools.FieldRefAccess<Weapon, WeaponStation?>("weaponStation");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Gun __instance, float ___fireInterval)
    {
        var client = Plugin.Client;
        var station = WeaponStationRef(__instance);

        if (client == null
            || ___fireInterval <= 0.2f
            || station == null
            || !client.BackSeat.Owns(__instance.attachedUnit, station.Number))
        {
            return;
        }

        client.BackSeat.Weapons.SingleFire(station.Number);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.RemoteFireAuto))]
internal static class WeaponStation_RemoteFireAuto
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponStation __instance, Unit owner)
    {
        return Plugin.Client?.BackSeat.Owns(owner, __instance.Number) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.RemoteFireSingle))]
internal static class WeaponStation_RemoteFireSingle
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponStation __instance, Unit owner)
    {
        return Plugin.Client?.BackSeat.Owns(owner, __instance.Number) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class Aircraft_UserCode_RpcLaunchMissile
{
    private static readonly MethodBase? Target = UserCode.Find(typeof(Aircraft), "RpcLaunchMissile");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null)
        {
            Plugin.Logger.LogError("Aircraft.UserCode_RpcLaunchMissile not found: a crew launch replays on its own client");
        }

        return Target != null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodBase TargetMethod()
    {
        return Target!;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance, byte stationIndex)
    {
        return Plugin.Client?.BackSeat.Owns(__instance, stationIndex) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class Aircraft_UserCode_RpcSetTurretVector
{
    private static readonly MethodBase? Target = UserCode.Find(typeof(Aircraft), "RpcSetTurretVector");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null)
        {
            Plugin.Logger.LogError("Aircraft.UserCode_RpcSetTurretVector not found: a gunner's turret lags their camera");
        }

        return Target != null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodBase TargetMethod()
    {
        return Target!;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance, byte weaponStationIndex)
    {
        return Plugin.Client?.BackSeat.Owns(__instance, weaponStationIndex) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.SingleRemoteFire))]
internal static class Unit_SingleRemoteFire
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Unit __instance, byte stationIndex)
    {
        return Plugin.Client?.Crew.BlocksStation(__instance, stationIndex) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), nameof(Aircraft.CmdLaunchMissile))]
internal static class Aircraft_CmdLaunchMissile
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance, byte stationIndex)
    {
        return Plugin.Client?.Crew.BlocksStation(__instance, stationIndex) != true;
    }
}
