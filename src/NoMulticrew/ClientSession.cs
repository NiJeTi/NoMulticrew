using Mirage;
using NoMulticrew.Networking;

namespace NoMulticrew;

internal sealed class ClientSession : IDisposable
{
    private readonly NetworkClient _client;
    private readonly INetworkPlayer _player;
    private readonly bool _serverAdvertisedCrew;
    private readonly Action<INetworkPlayer> _onAuthenticated;

    private bool _helloHandled;

    public MessageHandler MessageHandler { get; }

    public bool CrewConfirmed { get; private set; }

    public int SendsAllowed { get; private set; }

    public int SendsBlocked { get; private set; }

    private ClientSession(
        NetworkClient client,
        INetworkPlayer player,
        MessageHandler messageHandler,
        bool serverAdvertisedCrew
    )
    {
        _client = client;
        _player = player;
        _serverAdvertisedCrew = serverAdvertisedCrew;

        MessageHandler = messageHandler;

        CrewSerializers.EnsureRegistered();

        messageHandler.RegisterHandler<CrewWelcome>(OnWelcome, allowUnauthenticated: false);

        _onAuthenticated = OnAuthenticated;

        client.Authenticated.AddListener(_onAuthenticated);

        Plugin.Logger.LogInfo(
            serverAdvertisedCrew
                ? "Crew client session started: crew support was advertised"
                : "Crew client session started: no crew support advertised, staying dormant"
        );
    }

    public static ClientSession? TryCreate(NetworkClient client)
    {
        var messageHandler = client.MessageHandler;

        if (messageHandler == null)
        {
            return null;
        }

        var player = client.Player;

        if (player == null)
        {
            return null;
        }

        return new ClientSession(
            client, player, messageHandler, Discovery.TakeJoinInFlightAdvertisedCrew()
        );
    }

    public bool Send<T>(T message)
    {
        if (!CrewConfirmed)
        {
            SendsBlocked++;

            Plugin.Logger.LogInfo($"Blocked {typeof(T).Name}: the server has not confirmed crew support");

            return false;
        }

        return SendUnconfirmed(message);
    }

    public void Dispose()
    {
        if (SendsAllowed > 0 || SendsBlocked > 0)
        {
            Plugin.Logger.LogInfo(
                $"Client session send summary: allowed={SendsAllowed} blocked={SendsBlocked}"
            );
        }

        if (_client != null)
        {
            _client.Authenticated.RemoveListener(_onAuthenticated);
        }

        MessageHandler.UnregisterHandler<CrewWelcome>();
    }

    private bool SendUnconfirmed<T>(T message)
    {
        if (!_serverAdvertisedCrew)
        {
            SendsBlocked++;

            Plugin.Logger.LogWarning(
                $"Blocked {typeof(T).Name}: this server never advertised crew support. "
                + "This is a bug: a dormant session must send nothing."
            );

            return false;
        }

        if (!_client.IsConnected)
        {
            SendsBlocked++;

            Plugin.Logger.LogInfo($"Blocked {typeof(T).Name}: the client is no longer connected");

            return false;
        }

        _player.Send(message);
        SendsAllowed++;

        return true;
    }

    private void OnAuthenticated(INetworkPlayer player)
    {
        if (_helloHandled || !ReferenceEquals(_client.MessageHandler, MessageHandler))
        {
            return;
        }

        _helloHandled = true;

        if (!_serverAdvertisedCrew)
        {
            Plugin.Logger.LogInfo("No crew support advertised, so nothing is sent this session.");

            return;
        }

        if (SendUnconfirmed(new CrewHello(CrewSerializers.ProtocolVersion)))
        {
            Plugin.Logger.LogInfo("Sent CrewHello");
        }
        else
        {
            Plugin.Logger.LogWarning("CrewHello was blocked, so no crew will form this session.");
        }
    }

    private void OnWelcome(INetworkPlayer player, CrewWelcome message)
    {
        if (!message.Accepted)
        {
            CrewConfirmed = false;

            Plugin.Logger.LogWarning(
                $"Server declined crew support: its protocol is {message.ProtocolVersion}, ours is "
                + $"{CrewSerializers.ProtocolVersion}. Multicrew is off for this session."
            );

            return;
        }

        CrewConfirmed = true;

        Plugin.Logger.LogInfo($"Server confirmed crew support, protocol {message.ProtocolVersion}");
    }
}
