using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), nameof(Aircraft.StartEjectionSequence))]
internal static class Aircraft_StartEjectionSequence
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Aircraft __instance)
    {
        Plugin.Server?.Crew.Dissolve(__instance.persistentID);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), "UserCode_RpcLaunchMissile_1465828762")]
internal static class Aircraft_UserCode_RpcLaunchMissile
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance, byte stationIndex)
    {
        return Plugin.Client?.BackSeat.Owns(__instance, stationIndex) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), "UserCode_RpcSetTurretVector_-312569381")]
internal static class Aircraft_UserCode_RpcSetTurretVector
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance, byte weaponStationIndex)
    {
        return Plugin.Client?.BackSeat.Owns(__instance, weaponStationIndex) != true;
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

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), nameof(Aircraft.SetActiveStation))]
internal static class Aircraft_SetActiveStation
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance, byte stationIndex)
    {
        return Plugin.Client?.Crew.BlocksStation(__instance, stationIndex) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), "RpcSetActiveStation")]
internal static class Aircraft_RpcSetActiveStation
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Aircraft __instance, byte stationIndex)
    {
        Plugin.Server?.Crew.RecordPilotStation(__instance, stationIndex);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Aircraft), "UserCode_RpcSetActiveStation_1081017235")]
internal static class Aircraft_UserCode_RpcSetActiveStation
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance)
    {
        return !ReferenceEquals(Plugin.Client?.BackSeat.Aircraft, __instance);
    }
}
