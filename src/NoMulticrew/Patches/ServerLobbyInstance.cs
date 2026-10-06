using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Networking;
using NuclearOption.Networking.Lobbies;

namespace NoMulticrew.Patches;

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
