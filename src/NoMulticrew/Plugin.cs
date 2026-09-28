using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NoMulticrew.Server;
using NuclearOption.Networking;
using UnityEngine;

namespace NoMulticrew;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
internal sealed class Plugin : BaseUnityPlugin
{
    public new static ManualLogSource Logger { get; private set; } = null!;
    public static Settings Settings { get; private set; } = null!;

    internal static SeatTable Seats { get; private set; } = SeatTable.Defaults();

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
        CrewNetwork.EnsureHandlersRegistered();
        CrewServerCommands.EnsureRegistered();
        Advertisement.Tick();

        if (!MissionTracker.InMission)
        {
            return;
        }

        if (!MissionTracker.HasChanged())
        {
            return;
        }

        var table = SeatTableValidator.Validate(SeatTable.Load());

        table.WarnUnmatchedKeys(Resources.FindObjectsOfTypeAll<AircraftDefinition>());

        Seats = table;
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
