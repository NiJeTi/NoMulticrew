using System.Diagnostics.CodeAnalysis;
using Mirage;
using NoMulticrew.Networking;
using NoMulticrew.Networking.Messages;
using NoMulticrew.Seats;
using NuclearOption.DedicatedServer.Commands;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Server;

internal sealed class ServerSession : IDisposable
{
    private const string ServerCommandName = "multicrew";

    private readonly HashSet<INetworkPlayer> _validPlayers = [];
    private readonly HashSet<INetworkPlayer> _closedPilots = [];
    private readonly Dictionary<Type, Delegate> _handlers = [];

    private int[] _openSent = [];

    private readonly NetworkServer _server;
    private readonly bool _commandRegistered;

    public CrewRegistry Crew { get; private set; }

    public JoinRequests Requests { get; private set; }

    public CrewEconomy Economy { get; private set; }

    public CrewCommands Commands { get; private set; }

    public ServerSession(NetworkServer server)
    {
        _server = server;

        StartMission();

        Register<MulticrewHello>(OnHello);
        Register<CrewJoinRequest>((c, m) => Requests.OnRequest(c, m));
        Register<CrewJoinResponse>((c, m) => Requests.OnResponse(c, m));
        Register<CrewLeaveRequest>(OnLeave);
        Register<CrewCommand>((c, m) => Commands.OnCommand(c, m));
        Register<CrewAvailability>(OnAvailability);

        _commandRegistered = TryRegisterServerCommand();
    }

    public void Dispose()
    {
        _server.MessageHandler.UnregisterHandler<MulticrewHello>();
        _server.MessageHandler.UnregisterHandler<CrewJoinRequest>();
        _server.MessageHandler.UnregisterHandler<CrewJoinResponse>();
        _server.MessageHandler.UnregisterHandler<CrewLeaveRequest>();
        _server.MessageHandler.UnregisterHandler<CrewCommand>();
        _server.MessageHandler.UnregisterHandler<CrewAvailability>();
        _handlers.Clear();

        if (_commandRegistered)
        {
            ServerRemoteCommands.Instance.Commands.Remove(ServerCommandName);
        }
    }

    public void Tick()
    {
        Requests.Tick();
        SendOpenPilots();
    }

    public void EndMission()
    {
        Economy.PayAll();
        StartMission();
    }

    public bool TryGetPlayer(INetworkPlayer connection, [NotNullWhen(true)] out Player? player)
    {
        player = null;

        if (!_validPlayers.Contains(connection))
        {
            Plugin.Logger.LogError($"Crew message from {connection} before handshake");
            return false;
        }

        return connection.TryGetPlayer(out player);
    }

    public bool TakesCrew(Player pilot)
    {
        return _validPlayers.Contains(pilot.Owner) && !_closedPilots.Contains(pilot.Owner);
    }

    public void Receive<T>(T message)
        where T : struct, IMessage<T>
    {
        try
        {
            ((MessageDelegateWithPlayer<T>)_handlers[typeof(T)])(_server.LocalPlayer, message);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Exception handling {typeof(T).Name}: {e}");
        }
    }

    public void Notify(Player player, string text, NoticeTone tone, CrewCue cue)
    {
        SendToPlayer(player.Owner, new CrewNotice(text, tone, cue));
    }

    public bool SendToPlayer<T>(INetworkPlayer player, T message)
        where T : struct, IMessage<T>
    {
        if (!_validPlayers.Contains(player))
        {
            Plugin.Logger.LogError($"Attempt to send {typeof(T).Name} to a multicrew-incapable connection: {player}");
            return false;
        }

        Deliver(player, message);

        return true;
    }

    public void SendToAllCapable<T>(T message)
        where T : struct, IMessage<T>
    {
        foreach (var player in _validPlayers.ToArray())
        {
            Deliver(player, message);
        }
    }

    public void AnnounceKillAuthor(PersistentID killedId)
    {
        var author = Economy.KillAuthorOf(killedId);

        if (!ReferenceEquals(author, null))
        {
            SendToAllCapable(new CrewKillAuthor(killedId, author.PlayerIndex));
        }
    }

    public void ShowCrewHit(Player crew, Unit target, Vector3 relativePos)
    {
        SendToPlayer(
            crew.Owner,
            new CrewHit(
                target.persistentID, NetworkFloatHelper.CompressIfValid(relativePos, logErrors: false, "relativePos")
            )
        );
    }

    public void OnSceneReady(INetworkPlayer player)
    {
        if (_validPlayers.Contains(player))
        {
            Crew.SendRosters(player);
            Deliver(player, new CrewOpenPilots(_openSent));
        }
    }

    public void OnDisconnected(INetworkPlayer connection)
    {
        Remove(connection);
        _closedPilots.Remove(connection);

        if (connection.TryGetPlayer<Player>(out var player))
        {
            var seatedIn = Crew.AircraftOf(player);
            var name = player.GetDisplayName(PlayerNameContext.Other);

            Plugin.Logger.LogInfo(
                seatedIn is { } aircraftId
                    ? $"{name} disconnected, released {Texts.Roles.Wso} seat of {aircraftId}"
                    : $"{name} disconnected, not seated"
            );

            Requests.Forget(player);

            Crew.Release(player, forfeit: true);
            Crew.DissolvePilotedBy(player);
        }

        Commands.Forget(connection);
    }

    [MemberNotNull(nameof(Crew), nameof(Requests), nameof(Economy), nameof(Commands))]
    private void StartMission()
    {
        Crew = new CrewRegistry(this);
        Economy = new CrewEconomy(this);
        Commands = new CrewCommands(this);
        Requests = new JoinRequests(this);
    }

    private void Register<T>(MessageDelegateWithPlayer<T> handler)
        where T : struct, IMessage<T>
    {
        _handlers[typeof(T)] = handler;
        _server.MessageHandler.RegisterHandler(handler, allowUnauthenticated: false);
    }

    private void Deliver<T>(INetworkPlayer player, T message)
        where T : struct, IMessage<T>
    {
        if (ReferenceEquals(player, _server.LocalPlayer) && Plugin.Client is { Confirmed: true } client)
        {
            client.Receive(message);
            return;
        }

        player.Send(message);
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
            && SeatTable.IsValidExit(aircraft);

        Plugin.Logger.LogInfo(
            $"{player.GetDisplayName(PlayerNameContext.Other)} left {aircraftId.Value}: "
            + (valid ? "valid exit" : "bailout, escrow forfeited")
        );

        Crew.Release(player, forfeit: !valid);
    }

    private void OnAvailability(INetworkPlayer connection, CrewAvailability message)
    {
        if (!_validPlayers.Contains(connection))
        {
            return;
        }

        var changed = message.Accepting ? _closedPilots.Remove(connection) : _closedPilots.Add(connection);
        if (changed)
        {
            Plugin.Logger.LogInfo($"{connection} accepting crew requests: {message.Accepting}");
        }
    }

    private void SendOpenPilots()
    {
        var count = 0;
        var unchanged = true;

        foreach (var connection in _validPlayers)
        {
            if (!_closedPilots.Contains(connection)
                && connection.TryGetPlayer<Player>(out var player)
                && player.PlayerIndex > 0)
            {
                count++;
                unchanged &= Array.BinarySearch(_openSent, player.PlayerIndex) >= 0;
            }
        }

        if (unchanged && count == _openSent.Length)
        {
            return;
        }

        var indices = _validPlayers
            .Where(x => !_closedPilots.Contains(x))
            .Select(x => x.TryGetPlayer<Player>(out var player) ? player.PlayerIndex : 0)
            .Where(x => x > 0)
            .OrderBy(x => x)
            .ToArray();

        _openSent = indices;
        SendToAllCapable(new CrewOpenPilots(indices));
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
                    () => (true, $"multicrew-capable connections: {_validPlayers.Count}, crew entries: {Crew.Count}")
                );

                return ok
                    ? CommandResponse.Create(StatusCode.Success, description)
                    : CommandResponse.Create(StatusCode.CommandError, "Could not read multicrew state on the main thread.");
            }
        );
        instance.AddCommands([command]);

        return true;
    }
}