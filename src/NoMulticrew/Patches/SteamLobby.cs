using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Networking;
using NuclearOption.Networking.Lobbies;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(SteamLobby), nameof(SteamLobby.TryJoinLobby))]
internal static class SteamLobby_TryJoinLobby
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(LobbyInstance lobby)
    {
        if (lobby != null)
        {
            Discovery.NoteJoining(lobby);
        }
    }
}
