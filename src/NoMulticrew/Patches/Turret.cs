using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Turret), "FixedUpdate")]
internal static class Turret_FixedUpdate
{
    private static readonly Action<Turret, Vector3> AimTurret = AccessTools.MethodDelegate<Action<Turret, Vector3>>(
        GameMembers.Method(typeof(Turret), "AimTurret", [typeof(Vector3)])
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
            || !Plugin.SeatTable.IsManned(___aircraft, ___currentWeaponStation)
            || ___target != null)
        {
            return true;
        }

        Plugin.Client?.BackSeat.Weapons.Aim(__instance, ___aircraft, ___currentWeaponStation);

        AimTurret(__instance, ___manualVector);

        return false;
    }
}
