using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Seats;
using UnityEngine;

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

        if (!firing || !client.PilotSeat.Blocks(__instance, index))
        {
            return true;
        }

        return client.Crew.RefuseStation((Aircraft)__instance, index);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.RegisterHit))]
internal static class Unit_RegisterHit
{
    public static bool HidesNextDisplay { get; set; }

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
        HidesNextDisplay = false;

        var client = Plugin.Client;
        if (client != null
            && (GameManager.IsLocalAircraft(__instance) || ReferenceEquals(__instance, client.BackSeat.Aircraft)))
        {
            var station = SeatTable.StationOf(__instance, weaponInfo);

            if (client.PilotSeat.Blocks(__instance, station))
            {
                HidesNextDisplay = true;

                if (!(__instance.IsServer && IsServerAuthoritative(__instance.weaponStations[station])))
                {
                    return false;
                }
            }

            if (client.BackSeat.Owns(__instance, station) && !__instance.IsServer)
            {
                var hud = SceneSingleton<CombatHUD>.i;
                if (hud != null)
                {
                    hud.DisplayHit(hitUnit.transform.TransformPoint(relativePos).ToGlobalPosition(), hitUnit);
                }

                client.BackSeat.Weapons.ClaimHit(hitUnit, relativePos, bulletVelocity, (byte)station);
                return false;
            }
        }

        var server = Plugin.Server;
        __state = server?.Economy.EnterHitContext(__instance, weaponInfo) == true;

        if (__state)
        {
            server!.ShowCrewHit(server.Economy.ContextCrew!, hitUnit, relativePos);
        }

        return true;
    }

    private static bool IsServerAuthoritative(WeaponStation station)
    {
        return station.Weapons.Any(x => x is Gun { ForceServerAuthority: true });
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
[HarmonyPatch(typeof(Unit), nameof(Unit.SingleRemoteFire))]
internal static class Unit_SingleRemoteFire
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Unit __instance, byte stationIndex)
    {
        return Plugin.Client?.PilotSeat.Blocks(__instance, stationIndex) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), "UserCode_CmdClaimHit_-1122942669")]
internal static class Unit_UserCode_CmdClaimHit
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Unit __instance)
    {
        Plugin.Server?.Economy.OnClaim(__instance);
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
[HarmonyPatch(typeof(Unit), nameof(Unit.ReportKilled))]
internal static class Unit_ReportKilled
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Unit __instance, out bool __state)
    {
        __state = Plugin.Server?.Economy.OpenKill(__instance) == true;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Finalizer(Unit __instance, bool __state)
    {
        if (__state)
        {
            Plugin.Server!.Economy.CloseKill(__instance);
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), "UserCode_RpcSetStationTargets_1363862903")]
internal static class Unit_UserCode_RpcSetStationTargets
{
    private delegate void StationTargetsSetter(WeaponStation station, ReadOnlySpan<PersistentID> targetIds);

    private static readonly StationTargetsSetter SetStationTargets = AccessTools.MethodDelegate<StationTargetsSetter>(
        AccessTools.Method(typeof(WeaponStation), nameof(WeaponStation.SetStationTargets))
    );

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Unit __instance, byte stationIndex, ReadOnlySpan<PersistentID> targetIDs)
    {
        var seat = Plugin.Client?.BackSeat;
        if (seat == null || !ReferenceEquals(seat.Aircraft, __instance) || seat.Owns(__instance, stationIndex))
        {
            return true;
        }

        if (stationIndex < __instance.weaponStations.Count)
        {
            SetStationTargets(__instance.weaponStations[stationIndex], targetIDs);
        }

        return false;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Unit __instance, byte stationIndex, ReadOnlySpan<PersistentID> targetIDs)
    {
        Plugin.Client?.Crew.RecordTargets(__instance, stationIndex, targetIDs);
    }
}
