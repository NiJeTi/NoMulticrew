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
    private const float RadarIntervalSeconds = 0.5f;

    private readonly HashSet<INetworkPlayer> _validPlayers = [];
    private readonly Dictionary<Player, float> _radarForwardedAt = [];

    private readonly NetworkServer _server;
    private readonly bool _commandRegistered;

    public CrewRegistry Crew { get; }

    public JoinRequests Requests { get; }

    public CrewEconomy Economy { get; }

    public ServerSession(NetworkServer server)
    {
        _server = server;

        Crew = new CrewRegistry(this);
        Economy = new CrewEconomy(Crew);
        Requests = new JoinRequests(this, Crew);

        _server.MessageHandler.RegisterHandler<MulticrewHello>(OnHello, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<CrewJoinRequest>(Requests.OnRequest, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<CrewJoinResponse>(Requests.OnResponse, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<CrewLeaveRequest>(OnLeave, allowUnauthenticated: false);
        _server.MessageHandler.RegisterHandler<CrewAction>(OnAction, allowUnauthenticated: false);
        _server.Disconnected.AddListener(OnDisconnected);

        _commandRegistered = TryRegisterServerCommand();

        CrewState.LogClassification();
    }

    public void Dispose()
    {
        _server.Disconnected.RemoveListener(OnDisconnected);
        _server.MessageHandler.UnregisterHandler<MulticrewHello>();
        _server.MessageHandler.UnregisterHandler<CrewJoinRequest>();
        _server.MessageHandler.UnregisterHandler<CrewJoinResponse>();
        _server.MessageHandler.UnregisterHandler<CrewLeaveRequest>();
        _server.MessageHandler.UnregisterHandler<CrewAction>();

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
        _radarForwardedAt.Clear();
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

    public void Notify(Player player, string text)
    {
        SendToPlayer(player.Owner, new CrewNotice(text));
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

    private void OnAction(INetworkPlayer connection, CrewAction message)
    {
        if (!TryGetPlayer(connection, out var sender))
        {
            return;
        }

        if (!Enum.IsDefined(typeof(CrewActionKind), message.Kind))
        {
            Plugin.Logger.LogWarning($"Unknown crew action {message.Kind} from {connection}");
            return;
        }

        if (!UnitRegistry.TryGetUnit<Aircraft>(message.AircraftId, out var aircraft)
            || aircraft.disabled
            || aircraft.Player == null)
        {
            Plugin.Logger.LogDebug($"Dropped crew {message.Kind} for {message.AircraftId}: no live piloted aircraft");
            return;
        }

        var role = Crew.RoleOf(sender, message.AircraftId);
        if (role == null)
        {
            Plugin.Logger.LogDebug(
                $"Dropped crew {message.Kind} for {message.AircraftId} from "
                + $"{sender.GetDisplayName(PlayerNameContext.Other)}, who has no seat in it"
            );
            return;
        }

        if (message.Kind != CrewActionKind.Radar
            && !CrewState.Owns(role.Value, aircraft, message.StationIndex))
        {
            Plugin.Logger.LogWarning(
                $"Crew {message.Kind} names station {message.StationIndex} of {message.AircraftId}, "
                + $"which a {role.Value} seat does not own"
            );
            return;
        }

        if (message.Kind == CrewActionKind.Radar)
        {
            var now = Time.realtimeSinceStartup;
            if (now - _radarForwardedAt.GetValueOrDefault(sender, float.NegativeInfinity) < RadarIntervalSeconds)
            {
                return;
            }

            _radarForwardedAt[sender] = now;
        }

        SendToPlayer(aircraft.Player.Owner, message);
    }

    private void OnDisconnected(INetworkPlayer connection)
    {
        Remove(connection);

        if (connection.TryGetPlayer<Player>(out var player))
        {
            Requests.Forget(player);
            _radarForwardedAt.Remove(player);
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