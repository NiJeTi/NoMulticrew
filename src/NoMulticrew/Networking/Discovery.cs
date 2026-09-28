using Mirage;
using NuclearOption.Networking.Lobbies;
using Steamworks;

namespace NoMulticrew.Networking;

internal static class Discovery
{
    private static readonly Dictionary<CSteamID, bool> CapableByLobby = [];

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
        CrewNetwork.ServerIsCrewCapable = IsCapable(lobby);

        Plugin.Logger.LogInfo(
            CrewNetwork.ServerIsCrewCapable
                ? $"Joining {lobby.LobbyId}: crew support advertised"
                : $"Joining {lobby.LobbyId}: no crew support advertised, staying dormant"
        );
    }

    public static void OnClientAuthenticated(INetworkPlayer player)
    {
        if (!CrewNetwork.ServerIsCrewCapable)
        {
            return;
        }

        if (CrewNetwork.SendToServer(new CrewHello(CrewSerializers.ProtocolVersion)))
        {
            Plugin.Logger.LogInfo("Sent CrewHello");
        }
        else
        {
            Plugin.Logger.LogWarning("CrewHello was blocked, so no crew will form this session.");
        }
    }

    public static void Reset()
    {
        Plugin.Logger.LogInfo(
            $"Session send summary: allowed={CrewNetwork.SendsAllowed} blocked={CrewNetwork.SendsBlocked}"
        );

        CrewNetwork.ResetCounters();

        CrewNetwork.ServerIsCrewCapable = false;
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
