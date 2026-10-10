using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Networking;
using NuclearOption.Networking.Lobbies;
using Steamworks;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(DedicatedServerKeyValues), "ApplyTags")]
internal static class DedicatedServerKeyValues_ApplyTags
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Dictionary<string, string> ___tags)
    {
        var text = Discovery.TryAppendTags(___tags);
        if (text == null)
        {
            return;
        }

        SteamGameServer.SetGameTags(text);
    }
}