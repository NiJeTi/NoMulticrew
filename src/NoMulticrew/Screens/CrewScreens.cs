using System.Reflection;
using HarmonyLib;
using NoMulticrew.Seats;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoMulticrew.Screens;

internal sealed class CrewScreens
{
    private const float QuadHeightDegrees = 22f;
    private const float QuadMaxWidthDegrees = 40f;

    public static readonly AccessTools.FieldRef<TacScreen, Material?> MaterialRef =
        AccessTools.FieldRefAccess<TacScreen, Material?>("screenMaterial");

    public static readonly MethodInfo CamToggle = GameMembers.Method(typeof(TacScreen), "TacScreen_OnCamToggle");

    public static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");

    private static readonly AccessTools.FieldRef<TacScreen, bool> RadarOnRef =
        AccessTools.FieldRefAccess<TacScreen, bool>("radarOn");

    private static readonly AccessTools.FieldRef<Cockpit, GameObject?> PrefabRef =
        AccessTools.FieldRefAccess<Cockpit, GameObject?>("tacScreenUIPrefab");

    private static readonly AccessTools.FieldRef<TargetCam, Renderer?> ScreenRendererRef =
        AccessTools.FieldRefAccess<TargetCam, Renderer?>("targetScreenRenderer");

    private static readonly AccessTools.FieldRef<MissileWarningLight, MissileWarning?> MissileWarningRef =
        AccessTools.FieldRefAccess<MissileWarningLight, MissileWarning?>("missileWarning");

    private static readonly MethodInfo AppDisable =
        GameMembers.Method(typeof(MFDAppManager), "HUDAppManager_OnUnitDisable");

    private static readonly MethodInfo LightDisable =
        GameMembers.Method(typeof(MissileWarningLight), "MissileWarningLights_OnDisable");

    private static readonly MethodInfo LightWarning =
        GameMembers.Method(typeof(MissileWarningLight), "MissileWarningLights_OnMissileWarning");

    private readonly ClientSession _session;

    private Aircraft? _seated;
    private TacScreen? _own;
    private MFDAppManager[] _apps = [];
    private MissileWarningLight[] _lights = [];
    private ScreenQuad? _quad;
    private CrewmateScreen? _crewmate;
    private Aircraft? _attempted;

    public CrewScreens(ClientSession session)
    {
        _session = session;
    }

    public static GameObject? PrefabOf(Aircraft aircraft, out Cockpit? cockpit)
    {
        cockpit = aircraft.GetComponentInChildren<Cockpit>(true);

        return cockpit != null ? PrefabRef(cockpit) : null;
    }

    public static Renderer? ScreenRendererOf(Aircraft aircraft)
    {
        var cam = aircraft.GetComponentInChildren<TargetCam>(true);

        return cam != null ? ScreenRendererRef(cam) : null;
    }

    public static void SyncRadar(TacScreen screen, Aircraft aircraft)
    {
        if (aircraft.radar == null)
        {
            return;
        }

        RadarOnRef(screen) = !aircraft.radar.activated;
    }

    public void Board(Aircraft aircraft, SeatDefinition seat, Transform viewPoint, Vector3 eye)
    {
        Clear();
        _seated = aircraft;

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
            SyncRadar(_own, aircraft);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to build the WSO's tactical screen: {e}");
            DestroyOwn(aircraft);
            return;
        }

        Plugin.Logger.LogDebug($"Built the WSO's tactical screen on {aircraft.definition.jsonKey}");

        if (seat.Screen != null)
        {
            BuildQuad(aircraft, seat.Screen, viewPoint, eye);
        }

        if (seat.Panel != null)
        {
            _crewmate = CrewmateScreen.Create(aircraft, Role.Pilot, seat.Panel);
        }
    }

    public void Leave()
    {
        var aircraft = _seated;
        if (aircraft is null)
        {
            return;
        }

        _seated = null;

        _crewmate?.Dispose();
        _crewmate = null;

        _quad?.Dispose();
        _quad = null;

        DestroyOwn(aircraft);
    }

    public void Clear()
    {
        Leave();

        _crewmate?.Dispose();
        _crewmate = null;
        _attempted = null;
    }

    public void Tick()
    {
        _quad?.Show(InCockpitOf(_seated));

        if (_seated is null)
        {
            TickPilot();
        }

        _crewmate?.Tick(_session.Crew);
    }

    private void TickPilot()
    {
        var aircraft = CrewedLocalAircraft();

        if (_crewmate != null && !ReferenceEquals(_crewmate.Aircraft, aircraft))
        {
            _crewmate.Dispose();
            _crewmate = null;
        }

        if (aircraft == null)
        {
            _attempted = null;
            return;
        }

        if (_crewmate != null || ReferenceEquals(_attempted, aircraft))
        {
            return;
        }

        _attempted = aircraft;
        _crewmate = CrewmateScreen.Create(aircraft, Role.Wso, Plugin.SeatTable.PanelOf(aircraft)!);
    }

    private Aircraft? CrewedLocalAircraft()
    {
        if (!_session.Confirmed || !GameManager.GetLocalAircraft(out var aircraft) || aircraft == null || aircraft.disabled)
        {
            return null;
        }

        return Plugin.SeatTable.PanelOf(aircraft) != null && _session.Crew.ClientState(aircraft).WsoAboard ? aircraft : null;
    }

    private void BuildQuad(Aircraft aircraft, ScreenPlacement placement, Transform viewPoint, Vector3 eye)
    {
        var material = MaterialRef(_own!);
        var source = ScreenRendererOf(aircraft);
        var texture = material != null ? material.GetTexture(EmissionMap) : null;
        if (material == null || source == null || texture == null)
        {
            Plugin.Logger.LogError($"{aircraft.definition.jsonKey} has no screen material, renderer or texture; the WSO gets no screen quad");
            return;
        }

        var uv = placement.Uv;
        var aspect = uv.width * texture.width / (uv.height * texture.height);

        var centre = placement.Centre - eye;
        var distance = centre.magnitude;
        var height = 2f * distance * Mathf.Tan(QuadHeightDegrees * 0.5f * Mathf.Deg2Rad);
        var width = Mathf.Min(height * aspect, 2f * distance * Mathf.Tan(QuadMaxWidthDegrees * 0.5f * Mathf.Deg2Rad));
        height = width / aspect;

        var normal = placement.Normal.normalized;
        var rotation = Quaternion.LookRotation(-normal, Vector3.up);
        var right = rotation * Vector3.right * (width / 2f);
        var up = rotation * Vector3.up * (height / 2f);

        _quad = new ScreenQuad(
            "NoMulticrew.WsoScreen",
            viewPoint,
            source.gameObject.layer,
            material,
            [centre - right + up, centre + right + up, centre + right - up, centre - right - up],
            [
                new Vector2(uv.xMin, uv.yMax),
                new Vector2(uv.xMax, uv.yMax),
                new Vector2(uv.xMax, uv.yMin),
                new Vector2(uv.xMin, uv.yMin),
            ],
            normal
        );

        _quad.Show(false);

        Plugin.Logger.LogDebug(
            $"WSO screen quad {width:F3} x {height:F3} m at {centre:F3} ({distance:F3} m) from the eye, uv {uv}"
        );
    }

    private static bool InCockpitOf(Aircraft? aircraft)
    {
        var camera = SceneSingleton<CameraStateManager>.i;

        return aircraft != null
            && camera != null
            && camera.currentState == camera.cockpitState
            && ReferenceEquals(camera.followingUnit, aircraft);
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
