using Mirage;
using NoMulticrew.Crew;
using NoMulticrew.Networking;
using NoMulticrew.Ui;

namespace NoMulticrew;

internal sealed class ClientSession : IDisposable
{
    private readonly NetworkClient _client;
    private readonly Controls _controls;
    private readonly bool _advertised;

    private INetworkPlayer? _server;

    public bool Confirmed => _server != null;

    public CrewState Crew { get; } = new();

    public CrewJoinPromptUi Prompt { get; }

    public CrewSeatList SeatList { get; }

    public ClientSession(NetworkClient client, Controls controls)
    {
        _client = client;
        _controls = controls;
        _advertised = Discovery.TakeCurrentLobbyState();

        Prompt = new CrewJoinPromptUi(this);
        SeatList = new CrewSeatList(this);

        _client.MessageHandler.RegisterHandler<MulticrewWelcome>(OnWelcome, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<MulticrewState>(OnState, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<MulticrewJoinPrompt>(OnJoinPrompt, allowUnauthenticated: false);
        _client.MessageHandler.RegisterHandler<MulticrewNotice>(OnNotice, allowUnauthenticated: false);
        _client.Authenticated.AddListener(OnAuthenticated);
    }

    public void Dispose()
    {
        _client.Authenticated.RemoveListener(OnAuthenticated);
        _client.MessageHandler.UnregisterHandler<MulticrewWelcome>();
        _client.MessageHandler.UnregisterHandler<MulticrewState>();
        _client.MessageHandler.UnregisterHandler<MulticrewJoinPrompt>();
        _client.MessageHandler.UnregisterHandler<MulticrewNotice>();
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

        if (!_client.IsConnected)
        {
            return false;
        }

        _server.Send(message);

        return true;
    }

    private void OnAuthenticated(INetworkPlayer player)
    {
        if (!_advertised && !Plugin.IsServer)
        {
            return;
        }

        player.Send(new MulticrewHello(MessageRegistry.ProtocolVersion));
    }

    private void OnWelcome(INetworkPlayer player, MulticrewWelcome message)
    {
        _server = player;

        Plugin.Logger.LogInfo($"Server confirmed multicrew support, protocol {message.ProtocolVersion}");
    }

    private void OnState(INetworkPlayer player, MulticrewState message)
    {
        if (!Confirmed)
        {
            return;
        }

        Crew.Apply(message);
    }

    private void OnJoinPrompt(INetworkPlayer player, MulticrewJoinPrompt message)
    {
        if (!Confirmed)
        {
            return;
        }

        if (Plugin.Settings.RejectAllRequests.Value)
        {
            Send(new MulticrewJoinResponse(message.RequestId, accepted: false));
            return;
        }

        Prompt.Show(message);
    }

    private void OnNotice(INetworkPlayer player, MulticrewNotice message)
    {
        if (!Confirmed)
        {
            return;
        }

        Prompt.ShowNotice(message.Text);
    }

    public void Tick()
    {
        Prompt.Tick();

        HandleInput();
    }

    private void HandleInput()
    {
        if (!Confirmed)
        {
            return;
        }

        if (_controls.IsLeaveSeatDown())
        {
            Send(new MulticrewLeaveRequest());
        }

        if (_controls.IsAcceptRequestDown())
        {
            Prompt.Accept();
        }

        if (_controls.IsDeclineRequestDown())
        {
            Prompt.Decline();
        }
    }
}
