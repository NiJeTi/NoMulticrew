using Mirage;
using NoMulticrew.Crew;
using NoMulticrew.Marks;
using NoMulticrew.Networking;
using NoMulticrew.Ui;
using UnityEngine;

namespace NoMulticrew;

internal sealed class ClientSession : IDisposable
{
    private const float RequestTimeoutSeconds = 12f;

    private readonly NetworkClient _client;
    private readonly bool _advertised;

    private INetworkPlayer? _server;

    private VirtualMFD? _mfd;
    private CrewScreen? _crewScreen;

    private (PersistentID AircraftId, byte SeatIndex, float SentAt)? _request;

    public bool Confirmed => _server != null;

    public CrewState Crew { get; } = new();

    public CrewKillFeed Kills { get; } = new();

    public bool HasRequest => _request != null;

    public CrewJoinPromptUi Prompt { get; }

    public BackSeat BackSeat { get; }

    public PilotSeat PilotSeat { get; }

    public CrewMarks Marks { get; }

    public ClientSession(NetworkClient client)
    {
        _client = client;
        _advertised = Discovery.TakeCurrentLobbyState();

        Prompt = new CrewJoinPromptUi(this);
        BackSeat = new BackSeat(this);
        PilotSeat = new PilotSeat(this);
        Marks = new CrewMarks(this);

        _client.MessageHandler.RegisterHandler<MulticrewWelcome>(OnWelcome, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<CrewRoster>(OnState, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<CrewJoinPrompt>(OnJoinPrompt, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<CrewNotice>(OnNotice, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<CrewTurretVector>(OnTurretVector, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<CrewLaunch>(OnLaunch, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<CrewKillAuthor>(OnKillAuthor, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<CrewHit>(OnHit, allowUnauthenticated: false);
        _client.Authenticated.AddListener(OnAuthenticated);
    }

    public void Dispose()
    {
        BackSeat.Dispose();
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

        _server.Send(message);

        return true;
    }

    public bool IsRequested(PersistentID aircraftId, byte seatIndex)
    {
        return _request is { } request && request.AircraftId == aircraftId && request.SeatIndex == seatIndex;
    }

    public void EndMission()
    {
        BackSeat.Leave(showMap: false);
        Crew.Clear();
        Kills.Clear();
        Prompt.Clear();
        _request = null;
    }

    public void RequestSeat(PersistentID aircraftId, byte seatIndex)
    {
        if (_request != null || !Send(new CrewJoinRequest(aircraftId, seatIndex)))
        {
            return;
        }

        _request = (aircraftId, seatIndex, Time.unscaledTime);
        Feedback.Play(CrewCue.Select);
    }

    public void SendCommand(CrewCommand message)
    {
        var server = Plugin.Server;
        if (server != null)
        {
            server.DispatchLocal(message);
            return;
        }

        Send(message);
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
            Prompt.ShowNotice($"Multicrew needs NoMulticrew {message.PluginVersion}");
            return;
        }

        _server = player;

        Plugin.Logger.LogInfo($"Server confirmed multicrew support, protocol {message.ProtocolVersion}");
    }

    private void OnState(INetworkPlayer player, CrewRoster message)
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

        Prompt.Show(message);
    }

    private void OnNotice(INetworkPlayer player, CrewNotice message)
    {
        if (!Confirmed)
        {
            return;
        }

        _request = null;
        Prompt.ShowNotice(message.Text);
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

        Prompt.Tick();
        BackSeat.Tick();
        PilotSeat.Tick();

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