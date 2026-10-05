using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mirage;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NoMulticrew.Theming;
using NuclearOption.Networking;

namespace NoMulticrew;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(Controls.GuidInputFramework)]
internal sealed class Plugin : BaseUnityPlugin
{
    public new static ManualLogSource Logger { get; private set; } = null!;

    public static Settings Settings { get; private set; } = null!;
    public static SeatTable SeatTable { get; private set; } = null!;
    public static MarkPalette Palette { get; private set; } = null!;

    public static ClientSession? Client { get; private set; }
    public static ServerSession? Server { get; private set; }

    public static bool IsServer => Server != null;

    private static NetworkManagerNuclearOption? _manager;

    private static bool _seatTableAudited;

    private static Controls _controls = null!;

    private Harmony? _harmony;

    private void Awake()
    {
        Logger = base.Logger;

        Settings = Settings.Init(Config);
        Palette = new MarkPalette();
        SeatTable = new SeatTable();

        MessageRegistry.RegisterAll();

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

        _controls = Controls.Init();

        Logger.LogInfo("Patch successful");
    }

    private void LateUpdate()
    {
        Attach();

        Server?.Tick();
        Client?.Tick();
    }

    private void OnGUI()
    {
        Client?.SeatList.Draw();
        Client?.Prompt.Draw();
    }

    private void OnDestroy()
    {
        DisposeServerSession();
        DisposeClientSession();

        _harmony?.UnpatchSelf();
    }

    private static void Attach()
    {
        var manager = NetworkManagerProvider.Current;
        if (manager == null || ReferenceEquals(manager, _manager))
        {
            return;
        }

        _manager = manager;

        manager.Server.Started.AddListener(() => StartServerSession(manager.Server));
        manager.Server.Started.AddListener(Discovery.Advertise);
        manager.Server.Stopped.AddListener(DisposeServerSession);

        manager.Client.Started.AddListener(() => StartClientSession(manager.Client));
        manager.Client.Disconnected.AddListener(_ => DisposeClientSession());
    }

    private static void StartServerSession(NetworkServer server)
    {
        DisposeServerSession();

        Server = new ServerSession(server);
        AuditSeatTable();
    }

    private static void StartClientSession(NetworkClient client)
    {
        DisposeClientSession();

        Client = new ClientSession(client, _controls);
        AuditSeatTable();
    }

    private static void DisposeServerSession()
    {
        Server?.Dispose();
        Server = null;
    }

    private static void DisposeClientSession()
    {
        Client?.Dispose();
        Client = null;
    }

    private static void AuditSeatTable()
    {
        if (_seatTableAudited)
        {
            return;
        }

        _seatTableAudited = true;
        SeatTable.Audit();
    }
}