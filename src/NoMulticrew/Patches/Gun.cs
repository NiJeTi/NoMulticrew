using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Gun), "SpawnBullet")]
internal static class Gun_SpawnBullet
{
    private static readonly AccessTools.FieldRef<Weapon, WeaponStation> WeaponStationRef =
        AccessTools.FieldRefAccess<Weapon, WeaponStation>("weaponStation");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Gun __instance, float ___fireInterval)
    {
        var client = Plugin.Client;
        var station = WeaponStationRef(__instance);

        if (client == null
            || ___fireInterval <= 0.2f
            || !client.BackSeat.Owns(__instance.attachedUnit, station.Number))
        {
            return;
        }

        client.BackSeat.Weapons.SingleFire(station.Number);
    }
}
