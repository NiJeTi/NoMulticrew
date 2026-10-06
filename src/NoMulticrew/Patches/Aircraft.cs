using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using NoMulticrew.Networking;

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
[HarmonyPatch]
internal static class Aircraft_UserCode_RpcLaunchMissile
{
    private static readonly MethodBase? Target = UserCode.Find(typeof(Aircraft), "RpcLaunchMissile");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null)
        {
            Plugin.Logger.LogError("Aircraft.UserCode_RpcLaunchMissile not found: a crew launch replays on its own client");
        }

        return Target != null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodBase TargetMethod()
    {
        return Target!;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(Aircraft __instance, byte stationIndex)
    {
        return Plugin.Client?.BackSeat.Owns(__instance, stationIndex) != true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class Aircraft_UserCode_RpcSetTurretVector
{
    private static readonly MethodBase? Target = UserCode.Find(typeof(Aircraft), "RpcSetTurretVector");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null)
        {
            Plugin.Logger.LogError("Aircraft.UserCode_RpcSetTurretVector not found: a gunner's turret lags their camera");
        }

        return Target != null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodBase TargetMethod()
    {
        return Target!;
    }

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
        var crew = Plugin.Client?.Crew;
        if (crew == null || !crew.BlocksStation(__instance, stationIndex))
        {
            return true;
        }

        return crew.RefuseStation(__instance, stationIndex);
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
