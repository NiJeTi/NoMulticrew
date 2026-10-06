using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Mirage;
using NuclearOption.Networking;

namespace NoMulticrew.Patches;

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

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(NetworkManagerNuclearOption), "OnServerDisconnect")]
internal static class NetworkManagerNuclearOption_OnServerDisconnect
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        var present = AccessTools.Method(
            typeof(NetworkManagerNuclearOption), "OnServerDisconnect", [typeof(INetworkPlayer)]
        ) != null;

        if (!present)
        {
            Plugin.Logger.LogError(
                "NetworkManagerNuclearOption.OnServerDisconnect not found: crew seats of disconnected players are not released"
            );
        }

        return present;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(INetworkPlayer networkPlayer)
    {
        try
        {
            Plugin.Server?.OnDisconnected(networkPlayer);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Crew cleanup for disconnected {networkPlayer} failed: {e}");
        }
    }
}
