using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Mirage;
using NuclearOption.Networking;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(MissionManager), nameof(MissionManager.SetMission))]
internal static class MissionManager_SetMission
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix()
    {
        Plugin.Server?.EndMission();
        Plugin.Client?.EndMission();
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.Networkdisabled), MethodType.Setter)]
internal static class Unit_SetNetworkdisabled
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Unit __instance, bool value)
    {
        if (value)
        {
            Plugin.Server?.Crew.Dissolve(__instance.persistentID);
        }
    }
}

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
[HarmonyPatch(typeof(Player), nameof(Player.SetAircraft))]
internal static class Player_SetAircraft
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Player __instance)
    {
        var server = Plugin.Server;
        if (server == null || server.Crew.AircraftOf(__instance) is not { } seatedIn)
        {
            return;
        }

        server.Economy.Settle(__instance, seatedIn, forfeit: true);
        server.Crew.Release(__instance);
        server.Notify(__instance, "Left the seat");
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(NetworkManagerNuclearOption), "HandleSceneReadyMessage")]
internal static class NetworkManagerNuclearOption_HandleSceneReadyMessage
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(INetworkPlayer player)
    {
        Plugin.Server?.OnSceneReady(player);
    }
}
