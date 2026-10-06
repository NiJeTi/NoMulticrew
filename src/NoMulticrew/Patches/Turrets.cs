using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Seats;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.SetStationActive))]
internal static class WeaponStation_SetStationActive
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponStation __instance, Aircraft aircraft)
    {
        if (!IsManned(aircraft, __instance))
        {
            return true;
        }

        foreach (var turret in __instance.Turrets)
        {
            turret.SetManual(true);
        }

        return false;
    }

    public static bool IsManned(Aircraft aircraft, WeaponStation station)
    {
        if (!station.HasTurret())
        {
            return false;
        }

        var server = Plugin.Server;
        if (server != null)
        {
            return server.Crew.Holder(aircraft, station.Number) != null;
        }

        return Plugin.Client is { } client && client.Crew.Holder(aircraft, station.Number) != SeatTable.Pilot;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Turret), "FixedUpdate")]
internal static class Turret_FixedUpdate
{
    private static readonly Action<Turret, Vector3> AimTurret = AccessTools.MethodDelegate<Action<Turret, Vector3>>(
        AccessTools.Method(typeof(Turret), "AimTurret", [typeof(Vector3)])
    );

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(
        Turret __instance,
        Aircraft ___aircraft,
        WeaponStation ___currentWeaponStation,
        bool ___manual,
        bool ___stowed,
        bool ___disabled,
        Unit? ___target,
        ref Vector3 ___manualVector
    )
    {
        if (!___manual
            || ___stowed
            || ___disabled
            || ___aircraft.disabled
            || !WeaponStation_SetStationActive.IsManned(___aircraft, ___currentWeaponStation)
            || ___target != null)
        {
            return true;
        }

        Plugin.Client?.BackSeat.Weapons.Aim(__instance, ___aircraft, ___currentWeaponStation);

        AimTurret(__instance, ___manualVector);

        return false;
    }
}
