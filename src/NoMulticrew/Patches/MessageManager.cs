using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(MessageManager), nameof(MessageManager.RpcKillMessage))]
internal static class MessageManager_RpcKillMessage
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(PersistentID killedID)
    {
        Plugin.Server?.AnnounceKillAuthor(killedID);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(MessageManager), "UserCode_RpcKillMessage_635947223")]
internal static class MessageManager_UserCode_RpcKillMessage
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(PersistentID killerID, PersistentID killedID, KillType killedType)
    {
        return Plugin.Client?.Kills.TryPrint(killerID, killedID, killedType) != true;
    }
}
