using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

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
[HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.LaunchMount))]
internal static class WeaponStation_LaunchMount
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(WeaponStation __instance, Unit owner, int ___weaponIndex)
    {
        Plugin.Server?.Economy.OnLaunch(owner, __instance, ___weaponIndex);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.SetStationActive))]
internal static class WeaponStation_SetStationActive
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponStation __instance, Aircraft aircraft)
    {
        if (Plugin.SeatStateOf(aircraft) is not { } state || !Plugin.SeatTable.IsManned(aircraft, __instance, state))
        {
            return true;
        }

        foreach (var turret in __instance.Turrets)
        {
            turret.SetManual(true);
        }

        return false;
    }
}