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
