using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Networking;
using NuclearOption.Networking.Lobbies;
using Steamworks;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(SteamLobby), nameof(SteamLobby.TryJoinLobby))]
internal static class SteamLobby_TryJoinLobby
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(LobbyInstance lobby)
    {
        Discovery.OnJoin(lobby);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(ServerLobbyInstance), nameof(ServerLobbyInstance.SetDetails))]
internal static class ServerLobbyInstance_SetDetails
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(ServerLobbyInstance __instance)
    {
        try
        {
            Discovery.NoteServerTags(__instance);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to read server game tags: {e}");
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(DedicatedServerKeyValues), "ApplyTags")]
internal static class DedicatedServerKeyValues_ApplyTags
{
    private static readonly AccessTools.FieldRef<DedicatedServerKeyValues, Dictionary<string, string>> TagsRef =
        AccessTools.FieldRefAccess<DedicatedServerKeyValues, Dictionary<string, string>>("tags");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(DedicatedServerKeyValues __instance)
    {
        var text = Discovery.TryAppendTags(TagsRef(__instance));
        if (text == null)
        {
            return;
        }

        SteamGameServer.SetGameTags(text);
    }
}
