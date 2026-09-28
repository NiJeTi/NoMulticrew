using Mirage;
using NoMulticrew.Networking;
using NoMulticrew.Server;
using NuclearOption.Networking;
using UnityEngine.Events;

namespace NoMulticrew;

internal sealed class ServerSession : IDisposable
{
    private readonly NetworkServer _server;
    private readonly HashSet<INetworkPlayer> _capable = [];
    private readonly UnityAction<INetworkPlayer> _onDisconnected;

    private bool _commandRegistered;

    public MessageHandler MessageHandler { get; }

    public int SendsAllowed { get; private set; }

    public int SendsBlocked { get; private set; }

    private ServerSession(NetworkServer server, MessageHandler messageHandler)
    {
        _server = server;

        MessageHandler = messageHandler;

        CrewSerializers.EnsureRegistered();

        messageHandler.RegisterHandler<CrewHello>(OnHello, allowUnauthenticated: false);

        _onDisconnected = Remove;

        server.Disconnected.AddListener(_onDisconnected);

        _commandRegistered = CrewServerCommands.TryRegister(this);

        Plugin.Logger.LogInfo("Crew server session started");
    }

    public static ServerSession? TryCreate(NetworkServer server)
    {
        var messageHandler = server.MessageHandler;

        return messageHandler == null ? null : new ServerSession(server, messageHandler);
    }

    public void Tick()
    {
        if (!_commandRegistered)
        {
            _commandRegistered = CrewServerCommands.TryRegister(this);
        }
    }

    public bool IsCrewCapable(INetworkPlayer? player)
    {
        return player != null && _capable.Contains(player);
    }

    public void Add(INetworkPlayer player)
    {
        if (_capable.Add(player))
        {
            Plugin.Logger.LogInfo($"Crew-capable connection added: {player} (total {_capable.Count})");
        }
    }

    public void Remove(INetworkPlayer player)
    {
        if (_capable.Remove(player))
        {
            Plugin.Logger.LogInfo($"Crew-capable connection removed: {player} (total {_capable.Count})");
        }
    }

    public string Describe()
    {
        if (_capable.Count == 0)
        {
            return "crew-capable connections: none";
        }

        var names = _capable.Select(player =>
            player.TryGetPlayer<Player>(out var gamePlayer)
                ? gamePlayer.GetDisplayName(PlayerNameContext.Other)
                : player.ToString()
        );

        return $"crew-capable connections ({_capable.Count}): {string.Join(", ", names)}";
    }

    public bool SendToPlayer<T>(INetworkPlayer player, T message)
    {
        if (!IsCrewCapable(player))
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

    public void Dispose()
    {
        Plugin.Logger.LogInfo(
            $"Server session send summary: allowed={SendsAllowed} blocked={SendsBlocked}"
        );

        if (_commandRegistered)
        {
            CrewServerCommands.Unregister();
        }

        if (_server != null)
        {
            _server.Disconnected.RemoveListener(_onDisconnected);
        }

        MessageHandler.UnregisterHandler<CrewHello>();

        _capable.Clear();
    }

    private void OnHello(INetworkPlayer player, CrewHello message)
    {
        var accepted = message.ProtocolVersion == CrewSerializers.ProtocolVersion;

        Add(player);

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
            Remove(player);
        }
    }
}
