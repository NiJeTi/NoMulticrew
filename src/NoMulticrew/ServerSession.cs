using Mirage;
using NoMulticrew.Networking;
using NuclearOption.DedicatedServer.Commands;

namespace NoMulticrew;

internal sealed class ServerSession : IDisposable
{
    private const string ServerCommandName = "multicrew";

    private readonly HashSet<INetworkPlayer> _validPlayers = [];

    private readonly NetworkServer _server;
    private readonly bool _commandRegistered;

    public ServerSession(NetworkServer server)
    {
        _server = server;

        MessageRegistry.RegisterAll();

        _server.MessageHandler.RegisterHandler<MulticrewHello>(OnHello, allowUnauthenticated: false);
        _server.Disconnected.AddListener(OnDisconnected);

        _commandRegistered = TryRegisterServerCommand();
    }

    public void Dispose()
    {
        _server.Disconnected.RemoveListener(OnDisconnected);
        _server.MessageHandler.UnregisterHandler<MulticrewHello>();

        if (_commandRegistered)
        {
            ServerRemoteCommands.Instance?.Commands.Remove(ServerCommandName);
        }
    }

    private void Add(INetworkPlayer player)
    {
        if (_validPlayers.Add(player))
        {
            Plugin.Logger.LogDebug($"Multicrew-capable connection added: {player} (total {_validPlayers.Count})");
        }
    }

    private void Remove(INetworkPlayer player)
    {
        if (_validPlayers.Remove(player))
        {
            Plugin.Logger.LogDebug($"Multicrew-capable connection removed: {player} (total {_validPlayers.Count})");
        }
    }

    private void OnHello(INetworkPlayer player, MulticrewHello message)
    {
        if (message.ProtocolVersion != MessageRegistry.ProtocolVersion)
        {
            Plugin.Logger.LogWarning($"Multicrew protocol version mismatch from {player}");
            return;
        }

        Add(player);

        player.Send(new MulticrewWelcome(MessageRegistry.ProtocolVersion));
    }

    private void OnDisconnected(INetworkPlayer player)
    {
        Remove(player);
    }

    private bool TryRegisterServerCommand()
    {
        var instance = ServerRemoteCommands.Instance;
        if (instance == null)
        {
            return false;
        }

        if (instance.Commands.ContainsKey(ServerCommandName))
        {
            return false;
        }

        var command = new ServerCommand(
            ServerCommandName, (server, _) =>
            {
                var (ok, description) = server.RunOnMainThreadBlocking(
                    () => (true, $"multicrew-capable connections: {_validPlayers.Count}")
                );

                return ok
                    ? CommandResponse.Create(StatusCode.Success, description)
                    : CommandResponse.Create(
                        StatusCode.CommandError, "Could not read multicrew state on the main thread."
                    );
            }
        );
        instance.AddCommands([command]);

        return true;
    }
}