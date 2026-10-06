using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(TargetDetector), "TargetSearch")]
internal static class TargetDetector_TargetSearch
{
    private static readonly HashSet<TargetDetector> Idle = [];

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(TargetDetector __instance)
    {
        return Searches(__instance);
    }

    public static bool Searches(TargetDetector detector)
    {
        var client = Plugin.Client;
        var unit = detector.GetAttachedUnit();

        if (client == null
            || Plugin.IsServer
            || GameManager.IsLocalAircraft(unit)
            || ReferenceEquals(unit, client.BackSeat.Aircraft))
        {
            if (Idle.Count > 0 && Idle.Remove(detector))
            {
                Plugin.Logger.LogDebug($"Resumed the back seat's scans on {unit.unitName}");
            }

            return true;
        }

        if (Idle.Add(detector))
        {
            Idle.RemoveWhere(x => x == null);
            Plugin.Logger.LogDebug($"Stopped the back seat's scans on {unit.unitName}");
        }

        return false;
    }
}
