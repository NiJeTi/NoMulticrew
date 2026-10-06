using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoMulticrew.Screens;

internal sealed class CrewScreens
{
    public static readonly AccessTools.FieldRef<TacScreen, Material?> MaterialRef =
        AccessTools.FieldRefAccess<TacScreen, Material?>("screenMaterial");

    public static readonly MethodInfo? CamToggle = AccessTools.Method(typeof(TacScreen), "TacScreen_OnCamToggle");

    private static readonly AccessTools.FieldRef<Cockpit, GameObject?> PrefabRef =
        AccessTools.FieldRefAccess<Cockpit, GameObject?>("tacScreenUIPrefab");

    private static readonly AccessTools.FieldRef<MissileWarningLight, MissileWarning?> MissileWarningRef =
        AccessTools.FieldRefAccess<MissileWarningLight, MissileWarning?>("missileWarning");

    private static readonly MethodInfo? AppDisable =
        AccessTools.Method(typeof(MFDAppManager), "HUDAppManager_OnUnitDisable");

    private static readonly MethodInfo? LightDisable =
        AccessTools.Method(typeof(MissileWarningLight), "MissileWarningLights_OnDisable");

    private static readonly MethodInfo? LightWarning =
        AccessTools.Method(typeof(MissileWarningLight), "MissileWarningLights_OnMissileWarning");

    private Aircraft? _seated;
    private TacScreen? _own;
    private MFDAppManager[] _apps = [];
    private MissileWarningLight[] _lights = [];

    public static GameObject? PrefabOf(Aircraft aircraft, out Cockpit? cockpit)
    {
        cockpit = aircraft.GetComponentInChildren<Cockpit>(true);

        return cockpit != null ? PrefabRef(cockpit) : null;
    }

    public void Board(Aircraft aircraft)
    {
        Leave();
        _seated = aircraft;

        if (CamToggle == null || AppDisable == null || LightDisable == null || LightWarning == null)
        {
            Plugin.Logger.LogError("Tactical screen handlers not found; the WSO gets no tactical screen");
            return;
        }

        if (aircraft.targetCam == null)
        {
            Plugin.Logger.LogError("The crew's target camera did not attach; the WSO gets no tactical screen");
            return;
        }

        var prefab = PrefabOf(aircraft, out var cockpit);
        if (prefab == null || cockpit == null)
        {
            Plugin.Logger.LogError($"{aircraft.definition.jsonKey} has no tactical screen prefab");
            return;
        }

        try
        {
            _own = Object.Instantiate(prefab, cockpit.transform).GetComponent<TacScreen>();
            _apps = _own.GetComponentsInChildren<MFDAppManager>(true);
            _lights = _own.GetComponentsInChildren<MissileWarningLight>(true);
            _own.Initialize(aircraft, cockpit);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to build the WSO's tactical screen: {e}");
            DestroyOwn(aircraft);
            return;
        }

        Plugin.Logger.LogDebug($"Built the WSO's tactical screen on {aircraft.definition.jsonKey}");
    }

    public void Leave()
    {
        var aircraft = _seated;
        if (aircraft is null)
        {
            return;
        }

        _seated = null;
        DestroyOwn(aircraft);
    }

    public void Clear()
    {
        Leave();
    }

    private void DestroyOwn(Aircraft aircraft)
    {
        var own = _own;
        _own = null;

        foreach (var app in _apps)
        {
            aircraft.onDisableUnit -= AccessTools.MethodDelegate<Action<Unit>>(AppDisable, app);
        }

        foreach (var light in _lights)
        {
            aircraft.onDisableUnit -= AccessTools.MethodDelegate<Action<Unit>>(LightDisable, light);

            if (MissileWarningRef(light) is { } warning)
            {
                warning.onMissileWarning -=
                    AccessTools.MethodDelegate<Action<MissileWarning.OnMissileWarning>>(LightWarning, light);
            }
        }

        _apps = [];
        _lights = [];

        if (own is null)
        {
            return;
        }

        if (aircraft.targetCam is { } cam)
        {
            cam.onCamToggle -= AccessTools.MethodDelegate<Action<TargetCam.OnCamToggle>>(CamToggle, own);
        }

        if (own != null)
        {
            Object.Destroy(own.gameObject);
        }
    }
}
