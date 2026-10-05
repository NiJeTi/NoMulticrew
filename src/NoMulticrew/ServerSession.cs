using System.Diagnostics.CodeAnalysis;
using Mirage;
using NoMulticrew.Crew;
using NoMulticrew.Networking;
using NuclearOption.DedicatedServer.Commands;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew;

internal sealed class ServerSession : IDisposable
{
    private const string ServerCommandName = "multicrew";

    private readonly HashSet<INetworkPlayer> _validPlayers = [];

    private readonly NetworkServer _server;
    private readonly bool _commandRegistered;

    public CrewRegistry Crew { get; }

    public JoinRequests Requests { get; }

    public CrewEconomy Economy { get; }

    public CrewCommands Commands { get; }

    public ServerSession(NetworkServer server)
    {
        _server = server;

        Crew = new CrewRegistry(this);
        Economy = new CrewEconomy(this, Crew);
        Commands = new CrewCommands(this, Crew, Economy);
        Requests = new JoinRequests(this, Crew);

        _server.MessageHandler.RegisterHandler<MulticrewHello>(OnHello, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<CrewJoinRequest>(Requests.OnRequest, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<CrewJoinResponse>(Requests.OnResponse, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<CrewLeaveRequest>(OnLeave, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<CrewCommand>(Commands.OnCommand, allowUnauthenticated: false);
        _server.Disconnected.AddListener(OnDisconnected);

        _commandRegistered = TryRegisterServerCommand();
    }

    public void Dispose()
    {
        _server.Disconnected.RemoveListener(OnDisconnected);
        _server.MessageHandler.UnregisterHandler<MulticrewHello>();
        _server.MessageHandler.UnregisterHandler<CrewJoinRequest>();
        _server.MessageHandler.UnregisterHandler<CrewJoinResponse>();
        _server.MessageHandler.UnregisterHandler<CrewLeaveRequest>();
        _server.MessageHandler.UnregisterHandler<CrewCommand>();

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
        Economy.Clear();
        Commands.Clear();
    }

    public bool TryGetPlayer(INetworkPlayer connection, [NotNullWhen(true)] out Player? player)
    {
        player = null;

        if (!_validPlayers.Contains(connection))
        {
            Plugin.Logger.LogError($"Crew message received from a connection that never said hello: {connection}");
            return false;
        }

        return connection.TryGetPlayer(out player);
    }

    public void DispatchLocal(CrewCommand message)
    {
        Commands.OnCommand(_server.LocalPlayer, message);
    }

    public void Notify(Player player, string text, CrewCue cue = CrewCue.None)
    {
        SendToPlayer(player.Owner, new CrewNotice(text, cue));
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

        player.Send(new MulticrewWelcome(MessageRegistry.ProtocolVersion, MyPluginInfo.PLUGIN_VERSION));

        if (message.PluginVersion != MyPluginInfo.PLUGIN_VERSION)
        {
            Plugin.Logger.LogWarning(
                $"{player} runs NoMulticrew {message.PluginVersion}, this server {MyPluginInfo.PLUGIN_VERSION}: not crew-capable"
            );
            return;
        }

        Add(player);
    }

    private void OnLeave(INetworkPlayer connection, CrewLeaveRequest message)
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

        var valid = UnitRegistry.TryGetUnit<Aircraft>(aircraftId.Value, out var aircraft)
            && JoinRequests.IsValidExit(aircraft);

        Plugin.Logger.LogInfo(
            $"{player.GetDisplayName(PlayerNameContext.Other)} leaves {aircraftId.Value} "
            + (valid ? "with a valid exit" : "by bailing out")
        );

        Economy.Settle(player, aircraftId.Value, forfeit: !valid);
        Crew.Release(player);
        Notify(player, valid ? "Left the seat" : "Bailed out", CrewCue.Deselect);
    }

    private void OnDisconnected(INetworkPlayer connection)
    {
        Remove(connection);

        if (connection.TryGetPlayer<Player>(out var player))
        {
            Requests.Forget(player);

            if (Crew.AircraftOf(player) is { } seatedIn)
            {
                Economy.Settle(player, seatedIn, forfeit: true);
            }

            Crew.Release(player);
            Crew.DissolvePilotedBy(player);
        }

        Commands.Forget(connection);
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