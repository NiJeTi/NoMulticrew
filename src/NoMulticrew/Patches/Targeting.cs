using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using NoMulticrew.Networking;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.TargetListChanged))]
internal static class WeaponManager_TargetListChanged
{
    private static readonly AccessTools.FieldRef<WeaponManager, Aircraft> AircraftRef =
        AccessTools.FieldRefAccess<WeaponManager, Aircraft>("aircraft");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(WeaponManager __instance)
    {
        var client = Plugin.Client;
        if (client == null)
        {
            return true;
        }

        var aircraft = AircraftRef(__instance);

        if (ReferenceEquals(client.BackSeat.Aircraft, aircraft))
        {
            client.BackSeat.Weapons.PushTargets();
            return false;
        }

        var station = __instance.currentWeaponStation;

        return station == null || !client.Crew.BlocksStation(aircraft, station.Number);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class Unit_UserCode_RpcSetStationTargets
{
    private delegate void StationTargetsSetter(WeaponStation station, ReadOnlySpan<PersistentID> targetIds);

    private static readonly MethodBase? Target = UserCode.Find(typeof(Unit), "RpcSetStationTargets");

    private static readonly StationTargetsSetter SetStationTargets = AccessTools.MethodDelegate<StationTargetsSetter>(
        AccessTools.Method(typeof(WeaponStation), nameof(WeaponStation.SetStationTargets))
    );

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null)
        {
            Plugin.Logger.LogError(
                "Unit.UserCode_RpcSetStationTargets not found: the pilot's targets overwrite the crew's, and no marks are shown"
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

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class Aircraft_UserCode_RpcSetActiveStation
{
    private static readonly MethodBase? Target = UserCode.Find(typeof(Aircraft), "RpcSetActiveStation");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null)
        {
            Plugin.Logger.LogError(
                "Aircraft.UserCode_RpcSetActiveStation not found: the pilot's weapon change resets the crew's selection"
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
    private static bool Prefix(Aircraft __instance)
    {
        return !ReferenceEquals(Plugin.Client?.BackSeat.Aircraft, __instance);
    }
}
