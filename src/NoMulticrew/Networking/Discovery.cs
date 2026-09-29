using System.Text;
using HarmonyLib;
using NuclearOption.Networking.Lobbies;
using Steamworks;

namespace NoMulticrew.Networking;

internal static class Discovery
{
    private const string LobbyKey = "nomulticrew";

    private const string TagKey = "nmc";

    private const string TagValue = "1";

    private const int TagByteBudget = 128;

    private static readonly AccessTools.FieldRef<SteamLobby, HostedLobbyInstance> HostedLobbyRef =
        AccessTools.FieldRefAccess<SteamLobby, HostedLobbyInstance>("_hostedLobby");

    private static readonly Dictionary<CSteamID, bool> LobbyStateCache = [];

    private static bool _currentLobbyState;

    public static string? TryAppendTags(IReadOnlyDictionary<string, string> tags)
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
            Plugin.Logger.LogError("Server is out of tag byte budget");
            return null;
        }

        return text;
    }

    public static void Advertise()
    {
        var steamLobby = SteamLobby.instance;
        if (steamLobby == null)
        {
            return;
        }

        var hosted = HostedLobbyRef(steamLobby);

        if (!hosted.IsValid)
        {
            return;
        }

        hosted.SetData(LobbyKey, TagValue);
    }

    public static void NoteServerTags(ServerLobbyInstance lobby)
    {
        var capable = lobby.details.GetGameTags().Split(',')
            .Any(tag => tag.Split('=') is [TagKey, TagValue]);

        LobbyStateCache[lobby.LobbyId] = capable;
    }

    private static bool IsCapable(LobbyInstance lobby)
    {
        if (lobby.DedicatedServer)
        {
            return LobbyStateCache.GetValueOrDefault(lobby.LobbyId);
        }

        return SteamMatchmaking.GetLobbyData(lobby.LobbyId, LobbyKey) == TagValue;
    }

    public static void OnJoin(LobbyInstance lobby)
    {
        _currentLobbyState = IsCapable(lobby);
    }

    public static bool TakeCurrentLobbyState()
    {
        var state = _currentLobbyState;
        _currentLobbyState = false;
        return state;
    }
}