using Mirage;
using NoMulticrew.Client.Screens;
using NoMulticrew.Client.Ui;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NoMulticrew.Server;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew.Client;

internal sealed class ClientSession : IDisposable
{
    private const float RequestTimeoutSeconds = JoinRequests.TimeoutSeconds + 2f;

    private readonly NetworkClient _client;
    private readonly bool _advertised;

    private INetworkPlayer? _server;

    private VirtualMFD? _mfd;
    private CrewScreen? _crewScreen;

    private (PersistentID AircraftId, float SentAt)? _request;

    private int[] _closedPilots = [];

    private readonly Dictionary<Type, Delegate> _handlers = [];

    public bool Confirmed => _server != null;

    public CrewState Crew { get; } = new();

    public CrewKillFeed Kills { get; } = new();

    public bool HasRequest => _request != null;

    public CrewNotices Notices { get; }

    public BackSeat BackSeat { get; }

    public PilotSeat PilotSeat { get; }

    public CrewMarks Marks { get; }

    public CrewScreens Screens { get; }

    public WeaponWheel Wheel { get; } = new();

    public ClientSession(NetworkClient client)
    {
        _client = client;
        _advertised = Discovery.TakeCurrentLobbyState();

        Notices = new CrewNotices(this);
        BackSeat = new BackSeat(this);
        PilotSeat = new PilotSeat(this);
        Marks = new CrewMarks(this);
        Screens = new CrewScreens(this);

        Register<MulticrewWelcome>(OnWelcome);
        Register<CrewRoster>(OnRoster);
        Register<CrewJoinPrompt>(OnJoinPrompt);
        Register<CrewNotice>(OnNotice);
        Register<CrewTurretVector>(OnTurretVector);
        Register<CrewLaunch>(OnLaunch);
        Register<CrewKillAuthor>(OnKillAuthor);
        Register<CrewHit>(OnHit);
        Register<CrewClosedPilots>(OnClosedPilots);
        Plugin.Settings.RejectAllRequests.SettingChanged += OnRejectAllRequestsChanged;
        _client.Authenticated.AddListener(OnAuthenticated);
    }

    public void Dispose()
    {
        BackSeat.Dispose();
        Screens.Clear();
        Marks.Dispose();
        _crewScreen?.Dispose();
        _client.Authenticated.RemoveListener(OnAuthenticated);
        _client.MessageHandler.UnregisterHandler<MulticrewWelcome>();
        _client.MessageHandler.UnregisterHandler<CrewRoster>();
        _client.MessageHandler.UnregisterHandler<CrewJoinPrompt>();
        _client.MessageHandler.UnregisterHandler<CrewNotice>();
        _client.MessageHandler.UnregisterHandler<CrewTurretVector>();
        _client.MessageHandler.UnregisterHandler<CrewLaunch>();
        _client.MessageHandler.UnregisterHandler<CrewKillAuthor>();
        _client.MessageHandler.UnregisterHandler<CrewHit>();
        _client.MessageHandler.UnregisterHandler<CrewClosedPilots>();
        _handlers.Clear();
        Plugin.Settings.RejectAllRequests.SettingChanged -= OnRejectAllRequestsChanged;
    }

    public void Receive<T>(T message)
        where T : struct, IMessage<T>
    {
        try
        {
            ((MessageDelegateWithPlayer<T>)_handlers[typeof(T)])(_server!, message);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Exception handling {typeof(T).Name}: {e}");
        }
    }

    public void AttachMfd(VirtualMFD mfd)
    {
        _crewScreen?.Dispose();
        _crewScreen = null;
        _mfd = mfd;
    }

    public bool Send<T>(T message)
        where T : struct, IMessage<T>
    {
        if (_server == null)
        {
            Plugin.Logger.LogWarning(
                $"Refused to send {typeof(T).Name}: the server has not confirmed multicrew support"
            );

            return false;
        }

        if (Plugin.Server is { } server)
        {
            server.Receive(message);
            return true;
        }

        _server.Send(message);

        return true;
    }

    public Role? LocalRole(Aircraft? aircraft)
    {
        if (aircraft == null)
        {
            return null;
        }

        if (ReferenceEquals(BackSeat.Aircraft, aircraft))
        {
            return Role.Wso;
        }

        return GameManager.IsLocalAircraft(aircraft) ? Role.Pilot : null;
    }

    public bool IsRequested(PersistentID aircraftId)
    {
        return _request is { } request && request.AircraftId == aircraftId;
    }

    public bool TakesCrew(Player pilot)
    {
        return !_closedPilots.Contains(pilot.PlayerIndex);
    }

    public void EndMission()
    {
        BackSeat.Leave(showMap: false);
        Screens.Clear();
        Crew.Clear();
        Kills.Clear();
        Notices.Clear();
        Wheel.Clear();
        _request = null;
    }

    public void RequestSeat(PersistentID aircraftId)
    {
        if (_request != null)
        {
            return;
        }

        _request = (aircraftId, Time.unscaledTime);
        Feedback.Play(CrewCue.Select);

        if (!Send(new CrewJoinRequest(aircraftId)))
        {
            _request = null;
        }
    }

    private void Register<T>(MessageDelegateWithPlayer<T> handler)
        where T : struct, IMessage<T>
    {
        _handlers[typeof(T)] = handler;
        _client.MessageHandler.RegisterHandler(handler, allowUnauthenticated: false);
    }

    private void OnAuthenticated(INetworkPlayer player)
    {
        if (!_advertised && !Plugin.IsServer)
        {
            Plugin.Logger.LogDebug("Server does not advertise multicrew; staying vanilla");
            return;
        }

        player.Send(new MulticrewHello(MessageRegistry.ProtocolVersion, MyPluginInfo.PLUGIN_VERSION));
    }

    private void OnWelcome(INetworkPlayer player, MulticrewWelcome message)
    {
        if (message.PluginVersion != MyPluginInfo.PLUGIN_VERSION)
        {
            Plugin.Logger.LogWarning(
                $"Server runs NoMulticrew {message.PluginVersion}, this client {MyPluginInfo.PLUGIN_VERSION}: multicrew is off"
            );
            Notices.ShowNotice($"Multicrew needs NoMulticrew {message.PluginVersion}");
            return;
        }

        _server = player;

        Plugin.Logger.LogInfo($"Server confirmed multicrew support, protocol {message.ProtocolVersion}");

        SendAvailability();
    }

    private void OnRejectAllRequestsChanged(object? sender, EventArgs e)
    {
        SendAvailability();
    }

    private void SendAvailability()
    {
        if (Confirmed)
        {
            Send(new CrewAvailability(!Plugin.Settings.RejectAllRequests.Value));
        }
    }

    private void OnClosedPilots(INetworkPlayer player, CrewClosedPilots message)
    {
        if (Confirmed)
        {
            _closedPilots = message.PlayerIndices;
        }
    }

    private void OnRoster(INetworkPlayer player, CrewRoster message)
    {
        if (!Confirmed)
        {
            return;
        }

        Crew.Apply(message);
    }

    private void OnJoinPrompt(INetworkPlayer player, CrewJoinPrompt message)
    {
        if (!Confirmed)
        {
            return;
        }

        if (Plugin.Settings.RejectAllRequests.Value)
        {
            Send(new CrewJoinResponse(message.RequestId, accepted: false));
            return;
        }

        Notices.Show(message);
    }

    private void OnNotice(INetworkPlayer player, CrewNotice message)
    {
        if (!Confirmed)
        {
            return;
        }

        _request = null;
        Notices.ShowNotice(message.Text);
        Feedback.Play(message.Cue);
    }

    private void OnTurretVector(INetworkPlayer player, CrewTurretVector message)
    {
        if (Confirmed)
        {
            PilotSeat.OnTurretVector(message);
        }
    }

    private void OnLaunch(INetworkPlayer player, CrewLaunch message)
    {
        if (Confirmed)
        {
            PilotSeat.OnLaunch(message);
        }
    }

    private void OnKillAuthor(INetworkPlayer player, CrewKillAuthor message)
    {
        if (Confirmed)
        {
            Kills.Record(message.KilledId, message.PlayerIndex);
        }
    }

    private void OnHit(INetworkPlayer player, CrewHit message)
    {
        var hud = SceneSingleton<CombatHUD>.i;

        if (!Confirmed
            || BackSeat.Aircraft == null
            || hud == null
            || !UnitRegistry.TryGetUnit(message.TargetId, out var target)
            || !NetworkFloatHelper.TryDecompress(message.RelativePos, out var relativePos, logErrors: false, "relativePos"))
        {
            return;
        }

        hud.DisplayHit(target.transform.TransformPoint(relativePos).ToGlobalPosition(), target);
    }

    public void Tick()
    {
        if (_request is { } pending && Time.unscaledTime - pending.SentAt > RequestTimeoutSeconds)
        {
            _request = null;
        }

        BackSeat.Tick();
        PilotSeat.Tick();
        Screens.Tick();

        if (Confirmed)
        {
            Marks.Tick();
        }

        if (Confirmed && _crewScreen == null && _mfd != null && GameManager.gameState != GameState.SinglePlayer)
        {
            var mfd = _mfd;
            _mfd = null;

            try
            {
                _crewScreen = CrewScreen.Create(this, mfd);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Failed to create the crew screen: {e}");
            }
        }

        _crewScreen?.Tick();
    }
}