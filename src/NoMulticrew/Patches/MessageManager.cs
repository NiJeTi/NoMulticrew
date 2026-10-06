using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using NoMulticrew.Networking;

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
[HarmonyPatch]
internal static class MessageManager_UserCode_RpcKillMessage
{
    private static readonly MethodBase? Target = UserCode.Find(typeof(MessageManager), "RpcKillMessage");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null)
        {
            Plugin.Logger.LogError("MessageManager.UserCode_RpcKillMessage not found: the killfeed names the pilot for crew kills");
        }

        return Target != null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodBase TargetMethod()
    {
        return Target!;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(PersistentID killerID, PersistentID killedID, KillType killedType)
    {
        return Plugin.Client?.Kills.TryPrint(killerID, killedID, killedType) != true;
    }
}
