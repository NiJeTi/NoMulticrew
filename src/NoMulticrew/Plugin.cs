using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mirage;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;

namespace NoMulticrew;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
internal sealed class Plugin : BaseUnityPlugin
{
    public new static ManualLogSource Logger { get; private set; } = null!;
    public static Settings Settings { get; private set; } = null!;

    private static readonly SeatTable DefaultSeats = SeatTable.Defaults();

    private static ClientSession? _clientSession;
    private static ServerSession? _serverSession;
    private static MissionState? _missionState;

    internal static ClientSession? Client => _clientSession;
    internal static ServerSession? Server => _serverSession;

    internal static SeatTable Seats => _missionState?.Seats ?? DefaultSeats;

    public static bool IsServer
    {
        get
        {
            if (!CrewNetwork.NetworkManagerLoaded)
            {
                return false;
            }

            var manager = NetworkManagerNuclearOption.i;

            return manager != null && manager.Server != null && manager.Server.Active;
        }
    }

    private Harmony _harmony = null!;

    private void Awake()
    {
        Logger = base.Logger;
        Settings = Settings.Init(Config);

        if (!Settings.Enabled.Value)
        {
            enabled = false;
            Logger.LogInfo("Disabled by configuration; nothing patched.");
            return;
        }

        try
        {
            _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            _harmony.PatchAll();
        }
        catch (Exception e)
        {
            enabled = false;
            Logger.LogError($"Failed to patch: {e}");
            return;
        }

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded");
    }

    private void LateUpdate()
    {
        UpdateState();

        _serverSession?.Tick();

        Advertisement.Tick();
    }

    private void OnDestroy()
    {
        DisposeSessions();

        _missionState?.Dispose();
        _missionState = null;

        _harmony?.UnpatchSelf();
    }

    private static void UpdateState()
    {
        UpdateMissionState();

        var manager = CrewNetwork.NetworkManagerLoaded ? NetworkManagerNuclearOption.i : null;

        if (manager == null)
        {
            DisposeSessions();

            return;
        }

        UpdateServerSession(manager.Server);
        UpdateClientSession(manager.Client);
    }

    private static void UpdateMissionState()
    {
        if (MissionTracker.HasChanged())
        {
            var previous = _missionState;

            _missionState = null;
            previous?.Dispose();
        }

        _missionState ??= MissionState.TryCreate();
    }

    private static void UpdateServerSession(NetworkServer server)
    {
        if (server != null && _serverSession != null
            && ReferenceEquals(_serverSession.MessageHandler, server.MessageHandler))
        {
            return;
        }

        var previous = _serverSession;

        _serverSession = null;
        previous?.Dispose();

        _serverSession = server == null ? null : ServerSession.TryCreate(server);
    }

    private static void UpdateClientSession(NetworkClient client)
    {
        if (client != null && _clientSession != null
            && ReferenceEquals(_clientSession.MessageHandler, client.MessageHandler))
        {
            return;
        }

        var previous = _clientSession;

        _clientSession = null;
        previous?.Dispose();

        _clientSession = client == null ? null : ClientSession.TryCreate(client);
    }

    private static void DisposeSessions()
    {
        _serverSession?.Dispose();
        _serverSession = null;

        _clientSession?.Dispose();
        _clientSession = null;
    }
}
