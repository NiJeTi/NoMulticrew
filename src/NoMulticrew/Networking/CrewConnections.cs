using Mirage;
using NuclearOption.Networking;

namespace NoMulticrew.Networking;

internal static class CrewConnections
{
    private static readonly HashSet<INetworkPlayer> Capable = [];

    public static int Count => Capable.Count;

    public static bool IsCrewCapable(INetworkPlayer? player)
    {
        return player != null && Capable.Contains(player);
    }

    public static void Add(INetworkPlayer player)
    {
        if (Capable.Add(player))
        {
            Plugin.Logger.LogInfo($"Crew-capable connection added: {player} (total {Capable.Count})");
        }
    }

    public static void Remove(INetworkPlayer player)
    {
        if (Capable.Remove(player))
        {
            Plugin.Logger.LogInfo($"Crew-capable connection removed: {player} (total {Capable.Count})");
        }
    }

    public static void Clear()
    {
        Capable.Clear();
    }

    public static string Describe()
    {
        if (Capable.Count == 0)
        {
            return "crew-capable connections: none";
        }

        var names = Capable.Select(player =>
            player.TryGetPlayer<NuclearOption.Networking.Player>(out var gamePlayer)
                ? gamePlayer.GetDisplayName(NuclearOption.Networking.PlayerNameContext.Other)
                : player.ToString()
        );

        return $"crew-capable connections ({Capable.Count}): {string.Join(", ", names)}";
    }
}
