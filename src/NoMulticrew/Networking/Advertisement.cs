using System.Text;
using HarmonyLib;
using NuclearOption.Networking.Lobbies;
using Steamworks;

namespace NoMulticrew.Networking;

internal static class Advertisement
{
    public const string LobbyKey = "nomulticrew";

    public const string TagKey = "nmc";

    public const string TagValue = "1";

    private const int TagByteBudget = 128;

    private static CSteamID _advertisedLobby = CSteamID.Nil;

    private static readonly AccessTools.FieldRef<SteamLobby, HostedLobbyInstance> HostedLobbyRef =
        AccessTools.FieldRefAccess<SteamLobby, HostedLobbyInstance>("_hostedLobby");

    public static string BuildTagString(IReadOnlyDictionary<string, string> tags)
    {
        var builder = new StringBuilder();

        foreach (var pair in tags)
        {
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append(',');
        }

        builder.Append(TagKey).Append('=').Append(TagValue).Append(',');

        var text = builder.ToString();

        if (Encoding.UTF8.GetByteCount(text) > TagByteBudget)
        {
            Plugin.Logger.LogError(
                $"Server tags with the crew pair are {Encoding.UTF8.GetByteCount(text)} bytes, "
                + $"over Steam's {TagByteBudget}. Advertisement skipped; crews cannot form here."
            );

            return "";
        }

        return text;
    }

    public static void Tick()
    {
        if (!Plugin.Settings.AdvertiseCrewSupport.Value)
        {
            return;
        }

        var steamLobby = SteamLobby.instance;

        if (steamLobby == null)
        {
            return;
        }

        var hosted = HostedLobbyRef(steamLobby);

        if (!hosted.IsValid)
        {
            _advertisedLobby = CSteamID.Nil;

            return;
        }

        if (hosted.Id == _advertisedLobby)
        {
            return;
        }

        hosted.SetData(LobbyKey, TagValue);
        _advertisedLobby = hosted.Id;

        Plugin.Logger.LogInfo($"Advertised crew support in lobby data: {LobbyKey}=1");
    }
}
