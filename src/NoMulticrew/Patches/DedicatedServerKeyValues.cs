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
    private static readonly AccessTools.FieldRef<DedicatedServerKeyValues, Dictionary<string, string>> TagsRef =
        AccessTools.FieldRefAccess<DedicatedServerKeyValues, Dictionary<string, string>>("tags");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(DedicatedServerKeyValues __instance)
    {
        if (!Plugin.Settings.AdvertiseCrewSupport.Value)
        {
            return;
        }

        var text = Advertisement.BuildTagString(TagsRef(__instance));

        if (text.Length == 0)
        {
            return;
        }

        SteamGameServer.SetGameTags(text);

        Plugin.Logger.LogInfo($"Advertised crew support in server tags: '{text}'");
    }
}
