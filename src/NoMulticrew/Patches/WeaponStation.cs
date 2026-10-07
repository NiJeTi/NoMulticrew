using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Seats;

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
            return server.Crew.WsoHolding(aircraft, station.Number) != null;
        }

        return Plugin.Client is { } client && client.Crew.Holder(aircraft, station.Number) != SeatTable.Pilot;
    }
}
