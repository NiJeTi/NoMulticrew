using NuclearOption.Networking.Lobbies;
using Steamworks;

namespace NoMulticrew.Networking;

internal static class Discovery
{
    private static readonly Dictionary<CSteamID, bool> CapableByLobby = [];

    private static bool _joinInFlightAdvertisedCrew;

    public static void NoteServerTags(CSteamID lobbyId, string rawTags)
    {
        var capable = false;

        foreach (var pair in rawTags.Split(','))
        {
            var parts = pair.Split('=');

            if (parts.Length == 2 && parts[0] == Advertisement.TagKey && parts[1] == Advertisement.TagValue)
            {
                capable = true;

                break;
            }
        }

        CapableByLobby[lobbyId] = capable;
    }

    public static bool IsCapable(LobbyInstance lobby)
    {
        if (IsForced(lobby.LobbyId))
        {
            return true;
        }

        if (lobby.DedicatedServer)
        {
            return CapableByLobby.TryGetValue(lobby.LobbyId, out var capable) && capable;
        }

        return SteamMatchmaking.GetLobbyData(lobby.LobbyId, Advertisement.LobbyKey) == Advertisement.TagValue;
    }

    public static void NoteJoining(LobbyInstance lobby)
    {
        _joinInFlightAdvertisedCrew = IsCapable(lobby);

        Plugin.Logger.LogInfo(
            _joinInFlightAdvertisedCrew
                ? $"Joining {lobby.LobbyId}: crew support advertised"
                : $"Joining {lobby.LobbyId}: no crew support advertised, staying dormant"
        );
    }

    public static bool TakeJoinInFlightAdvertisedCrew()
    {
        var advertised = _joinInFlightAdvertisedCrew;

        _joinInFlightAdvertisedCrew = false;

        return advertised;
    }

    private static bool IsForced(CSteamID lobbyId)
    {
        var forced = Plugin.Settings.ForceCapableServers.Value;

        if (string.IsNullOrWhiteSpace(forced))
        {
            return false;
        }

        var text = lobbyId.m_SteamID.ToString();

        return forced.Split(',').Any(entry => entry.Trim() == text);
    }
}
