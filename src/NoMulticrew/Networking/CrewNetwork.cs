using HarmonyLib;
using Mirage;
using NuclearOption.Networking;

namespace NoMulticrew.Networking;

internal static class CrewNetwork
{
    private static readonly AccessTools.FieldRef<ResourcesAsyncLoader<NetworkManagerNuclearOption>> NetworkManagerLoaderRef =
        AccessTools.StaticFieldRefAccess<ResourcesAsyncLoader<NetworkManagerNuclearOption>>(
            AccessTools.Field(typeof(NetworkManagerNuclearOption), "loader")
        );

    public static bool NetworkManagerLoaded => NetworkManagerLoaderRef().IsLoaded;

    public static bool ServerIsCrewCapable { get; set; }

    public static int SendsAllowed { get; private set; }

    public static int SendsBlocked { get; private set; }

    public static void ResetCounters()
    {
        SendsAllowed = 0;
        SendsBlocked = 0;
    }

    private static MessageHandler? _clientHandlersOn;
    private static MessageHandler? _serverHandlersOn;
    private static bool _listenersAdded;

    public static bool EnsureHandlersRegistered()
    {
        if (!NetworkManagerLoaded)
        {
            return false;
        }

        var manager = NetworkManagerNuclearOption.i;

        if (manager == null || manager.Client == null || manager.Server == null)
        {
            return false;
        }

        var clientHandler = manager.Client.MessageHandler;

        if (clientHandler != null && !ReferenceEquals(clientHandler, _clientHandlersOn))
        {
            RegisterClientHandlers(manager.Client);
            _clientHandlersOn = clientHandler;
        }

        var serverHandler = manager.Server.MessageHandler;

        if (serverHandler != null && !ReferenceEquals(serverHandler, _serverHandlersOn))
        {
            RegisterServerHandlers(manager.Server);
            _serverHandlersOn = serverHandler;
        }

        if (!_listenersAdded)
        {
            manager.Client.Authenticated.AddListener(Discovery.OnClientAuthenticated);
            manager.Client.Disconnected.AddListener(_ => Discovery.Reset());
            manager.Server.Disconnected.AddListener(CrewConnections.Remove);
            manager.Server.Stopped.AddListener(CrewConnections.Clear);

            _listenersAdded = true;
        }

        return _listenersAdded
            && _clientHandlersOn != null && ReferenceEquals(_clientHandlersOn, manager.Client.MessageHandler)
            && _serverHandlersOn != null && ReferenceEquals(_serverHandlersOn, manager.Server.MessageHandler);
    }

    public static void RegisterServerHandlers(NetworkServer server)
    {
        CrewSerializers.EnsureRegistered();

        server.MessageHandler.RegisterHandler<CrewHello>(OnHello, allowUnauthenticated: false);

        Plugin.Logger.LogInfo("Crew server handlers registered");
    }

    public static void RegisterClientHandlers(NetworkClient client)
    {
        CrewSerializers.EnsureRegistered();

        client.MessageHandler.RegisterHandler<CrewWelcome>(OnWelcome, allowUnauthenticated: false);

        Plugin.Logger.LogInfo("Crew client handlers registered");
    }

    public static bool SendToServer<T>(T message)
    {
        if (!ServerIsCrewCapable)
        {
            SendsBlocked++;

            Plugin.Logger.LogInfo($"Blocked {typeof(T).Name}: server has not advertised crew support");

            return false;
        }

        var client = NetworkManagerNuclearOption.i?.Client;

        if (client == null || !client.IsConnected || client.Player == null)
        {
            SendsBlocked++;

            Plugin.Logger.LogInfo($"Blocked {typeof(T).Name}: no connected client to send it on");

            return false;
        }

        client.Player.Send(message);
        SendsAllowed++;

        return true;
    }

    public static bool SendToPlayer<T>(INetworkPlayer player, T message)
    {
        if (!CrewConnections.IsCrewCapable(player))
        {
            SendsBlocked++;

            Plugin.Logger.LogWarning(
                $"Blocked {typeof(T).Name} to a connection outside the crew-capable set. "
                + "This is a bug: every crew send must be scoped."
            );

            return false;
        }

        player.Send(message);
        SendsAllowed++;

        return true;
    }

    private static void OnHello(INetworkPlayer player, CrewHello message)
    {
        var accepted = message.ProtocolVersion == CrewSerializers.ProtocolVersion;

        CrewConnections.Add(player);

        if (!accepted)
        {
            Plugin.Logger.LogWarning(
                $"Crew protocol mismatch from {player}: theirs {message.ProtocolVersion}, "
                + $"ours {CrewSerializers.ProtocolVersion}. Crews will not form for this player."
            );
        }

        SendToPlayer(player, new CrewWelcome(CrewSerializers.ProtocolVersion, accepted));

        if (!accepted)
        {
            CrewConnections.Remove(player);
        }
    }

    private static void OnWelcome(INetworkPlayer player, CrewWelcome message)
    {
        if (!message.Accepted)
        {
            ServerIsCrewCapable = false;

            Plugin.Logger.LogWarning(
                $"Server declined crew support: its protocol is {message.ProtocolVersion}, ours is "
                + $"{CrewSerializers.ProtocolVersion}. Multicrew is off for this session."
            );

            return;
        }

        Plugin.Logger.LogInfo($"Server confirmed crew support, protocol {message.ProtocolVersion}");
    }
}
