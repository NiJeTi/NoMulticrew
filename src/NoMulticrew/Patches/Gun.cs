using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Gun), "SpawnBullet")]
internal static class Gun_SpawnBullet
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Gun __instance, float ___fireInterval, WeaponStation ___weaponStation)
    {
        var client = Plugin.Client;

        if (client == null
            || ___fireInterval <= 0.2f
            || !client.BackSeat.Owns(__instance.attachedUnit, ___weaponStation.Number))
        {
            return;
        }

        client.BackSeat.Weapons.SingleFire(___weaponStation.Number);
    }
}