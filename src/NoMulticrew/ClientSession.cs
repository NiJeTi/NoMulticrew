using Mirage;
using NoMulticrew.Networking;

namespace NoMulticrew;

internal sealed class ClientSession : IDisposable
{
    private readonly NetworkClient _client;

    private bool _enabled;

    public ClientSession(NetworkClient client)
    {
        _client = client;
        _enabled = Discovery.TakeCurrentLobbyState();

        MessageRegistry.RegisterAll();

        _client.MessageHandler.RegisterHandler<MulticrewWelcome>(OnWelcome, allowUnauthenticated: false);
        _client.Authenticated.AddListener(OnAuthenticated);
    }

    public void Dispose()
    {
        _client.Authenticated.RemoveListener(OnAuthenticated);
        _client.MessageHandler.UnregisterHandler<MulticrewWelcome>();
    }

    private void OnAuthenticated(INetworkPlayer player)
    {
        if (!_enabled)
        {
            return;
        }

        player.Send(new MulticrewHello(MessageRegistry.ProtocolVersion));
    }

    private void OnWelcome(INetworkPlayer player, MulticrewWelcome message)
    {
        _enabled = true;
    }
}
