using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

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
        if (client == null)
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
