using System.Diagnostics.CodeAnalysis;
using Mirage;
using NoMulticrew.Crew;
using NoMulticrew.Networking;
using NuclearOption.DedicatedServer.Commands;
using NuclearOption.Networking;

namespace NoMulticrew;

internal sealed class ServerSession : IDisposable
{
    private const string ServerCommandName = "multicrew";

    private readonly HashSet<INetworkPlayer> _validPlayers = [];

    private readonly NetworkServer _server;
    private readonly bool _commandRegistered;

    public CrewRegistry Crew { get; }

    public JoinRequests Requests { get; }

    public ServerSession(NetworkServer server)
    {
        _server = server;

        Crew = new CrewRegistry(this);
        Requests = new JoinRequests(this, Crew);

        _server.MessageHandler.RegisterHandler<MulticrewHello>(OnHello, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<MulticrewJoinRequest>(Requests.OnRequest, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<MulticrewJoinResponse>(Requests.OnResponse, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<MulticrewLeaveRequest>(OnLeave, allowUnauthenticated: false);
        _server.Disconnected.AddListener(OnDisconnected);

        _commandRegistered = TryRegisterServerCommand();
    }

    public void Dispose()
    {
        _server.Disconnected.RemoveListener(OnDisconnected);
        _server.MessageHandler.UnregisterHandler<MulticrewHello>();
        _server.MessageHandler.UnregisterHandler<MulticrewJoinRequest>();
        _server.MessageHandler.UnregisterHandler<MulticrewJoinResponse>();
        _server.MessageHandler.UnregisterHandler<MulticrewLeaveRequest>();

        Clear();

        if (_commandRegistered)
        {
            ServerRemoteCommands.Instance.Commands.Remove(ServerCommandName);
        }
    }

    public void Tick()
    {
        Requests.Tick();
    }

    public void Clear()
    {
        Crew.Clear();
        Requests.Clear();
    }

    public bool TryGetPlayer(INetworkPlayer connection, [NotNullWhen(true)] out Player? player)
    {
        player = null;

        if (!_validPlayers.Contains(connection))
        {
            Plugin.Logger.LogError($"Multicrew message received from an invalid connection: {connection}");
            return false;
        }

        return connection.TryGetPlayer(out player);
    }

    public void Notify(Player player, string text)
    {
        SendToPlayer(player.Owner, new MulticrewNotice(text));
    }

    public bool SendToPlayer<T>(INetworkPlayer player, T message)
        where T : struct, IMessage<T>
    {
        if (!_validPlayers.Contains(player))
        {
            Plugin.Logger.LogError($"Attempt to send {typeof(T).Name} to a multicrew-incapable connection: {player}");

            return false;
        }

        player.Send(message);

        return true;
    }

    public void SendToAllCapable<T>(T message)
        where T : struct, IMessage<T>
    {
        foreach (var player in _validPlayers.ToArray())
        {
            SendToPlayer(player, message);
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

    private void OnLeave(INetworkPlayer connection, MulticrewLeaveRequest message)
    {
        if (!TryGetPlayer(connection, out var player))
        {
            return;
        }

        var aircraftId = Crew.AircraftOf(player);
        if (aircraftId == null)
        {
            return;
        }

        if (UnitRegistry.TryGetUnit(aircraftId.Value, out var unit)
            && unit is Aircraft aircraft
            && !JoinRequests.TryGetAirbase(aircraft, out _))
        {
            Notify(player, "The aircraft is not at an airbase");
            return;
        }

        Crew.Release(player);
        Notify(player, "Left the seat");
    }

    private void OnDisconnected(INetworkPlayer connection)
    {
        Remove(connection);

        if (connection.TryGetPlayer<Player>(out var player))
        {
            Requests.Forget(player);
            Crew.Release(player);
            Crew.DissolvePilotedBy(player);
        }
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