using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mirage;
using NoMulticrew.Client;
using NoMulticrew.Client.Theming;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NoMulticrew.Server;
using NuclearOption.Networking;
namespace NoMulticrew;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("NoWingmen", BepInDependency.DependencyFlags.SoftDependency)]
internal sealed class Plugin : BaseUnityPlugin
{
    public new static ManualLogSource Logger { get; private set; } = null!;

    public static Settings Settings { get; private set; } = null!;
    public static SeatTable SeatTable { get; private set; } = null!;
    public static MarkPalette Palette { get; private set; } = null!;

    public static ClientSession? Client { get; private set; }
    public static ServerSession? Server { get; private set; }

    public static bool IsServer => Server != null;

    public static SeatState? SeatStateOf(Aircraft aircraft)
    {
        return Server?.Crew.ServerState(aircraft) ?? Client?.Crew.ClientState(aircraft);
    }

    private static AccessTools.FieldRef<ResourcesAsyncLoader<NetworkManagerNuclearOption>> NetworkManagerLoaderRef = null!;

    private static NetworkManagerNuclearOption? _manager;

    private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

    private void Awake()
    {
        Logger = base.Logger;

        Settings = new Settings(Config);
        Palette = new MarkPalette();
        SeatTable = new SeatTable();

        try
        {
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (!type.ContainsGenericParameters)
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
            }

            NetworkManagerLoaderRef = AccessTools.StaticFieldRefAccess<ResourcesAsyncLoader<NetworkManagerNuclearOption>>(
                GameMembers.Field(typeof(NetworkManagerNuclearOption), "loader")
            );

            _harmony.PatchAll();
            MessageRegistry.RegisterAll();
        }
        catch (Exception e)
        {
            _harmony.UnpatchSelf();
            enabled = false;
            Logger.LogError($"NoMulticrew is disabled and the game runs vanilla: {e.GetBaseException().Message}\n{e}");
            return;
        }

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
        Client?.Notices.Draw();
    }

    private void OnDestroy()
    {
        DisposeServerSession();
        DisposeClientSession();

        _harmony.UnpatchSelf();
    }

    private static void Attach()
    {
        var manager = NetworkManagerLoaderRef().IsLoaded ? NetworkManagerNuclearOption.i : null;
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

        Client = new ClientSession(client);
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
        try
        {
            SeatTable.Audit();
        }
        catch (Exception e)
        {
            Logger.LogError($"Seat table audit failed: {e}");
        }
    }
}