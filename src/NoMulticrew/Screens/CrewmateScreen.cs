using HarmonyLib;
using NoMulticrew.Crew;
using NoMulticrew.Seats;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMulticrew.Screens;

internal sealed class CrewmateScreen : IDisposable
{
    private const float PanelLift = 0.002f;

    private static readonly AccessTools.FieldRef<TacScreen, GameObject?> TargetDisplayRef =
        AccessTools.FieldRefAccess<TacScreen, GameObject?>("targetCamDisplay");

    private static readonly AccessTools.FieldRef<TacScreen, GameObject?> LandingDisplayRef =
        AccessTools.FieldRefAccess<TacScreen, GameObject?>("landingCamDisplay");

    private readonly List<Unit> _targets = [];
    private readonly Role _seat;

    private GameObject? _holder;
    private TacScreen? _screen;
    private RenderTexture? _texture;
    private Material? _material;
    private Renderer? _source;
    private ScreenQuad? _overlay;
    private CrewmateCam? _camera;
    private Action<TargetCam.OnCamToggle>? _toggle;
    private bool _shown;
    private (int Station, int Ids, int Live, bool Overlay, string Camera) _logged = (int.MinValue, 0, 0, false, "");

    public Aircraft Aircraft { get; }

    private CrewmateScreen(Aircraft aircraft, Role seat)
    {
        Aircraft = aircraft;
        _seat = seat;
    }

    public static CrewmateScreen? Create(Aircraft aircraft, Role seat, PanelPlacement panel)
    {
        var screen = new CrewmateScreen(aircraft, seat);

        try
        {
            screen.Build(panel);
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to build the {SeatTable.Label(seat)}'s screen on the panel: {e}");
            screen.Dispose();

            return null;
        }

        Plugin.Logger.LogDebug(
            $"Built the {SeatTable.Label(seat)}'s screen on the panel of {aircraft.definition.jsonKey}"
        );

        return screen;
    }

    public void Tick(CrewState crew)
    {
        var overlay = _source != null && _source.enabled;
        _overlay?.Show(overlay);

        var state = crew.ClientState(Aircraft);
        var station = _seat == Role.Pilot ? state.PilotStation : state.WsoStation;
        var ids = crew.TargetsOf(Aircraft, station);

        _targets.Clear();

        for (var i = 0; i < ids.Count; i++)
        {
            if (UnitRegistry.TryGetUnit(ids[i], out var unit) && unit != null && !unit.disabled)
            {
                _targets.Add(unit);
            }
        }

        var active = _camera != null && _toggle != null && _screen != null && _camera.Tick(_targets);
        var camera = _camera == null ? "missing" : active ? "active" : "idle";
        var logged = (station, ids.Count, _targets.Count, overlay, camera);

        if (logged != _logged)
        {
            _logged = logged;
            Plugin.Logger.LogDebug(
                $"{SeatTable.Label(_seat)}'s screen on {Aircraft.definition.jsonKey}: station {station}, "
                + $"{ids.Count} target ids, {_targets.Count} live, overlay {(overlay ? "shown" : "hidden")}, camera {camera}"
            );
        }

        if (_toggle == null || active == _shown)
        {
            return;
        }

        _shown = active;
        _toggle(new TargetCam.OnCamToggle { enabled = active, camMode = TargetCam.CamMode.targetForward });
    }

    public void Dispose()
    {
        _overlay?.Dispose();
        _overlay = null;

        _camera?.Dispose();

        if (_holder != null)
        {
            Object.Destroy(_holder);
        }

        if (_material != null)
        {
            Object.Destroy(_material);
        }

        if (_texture != null)
        {
            Object.Destroy(_texture);
        }

        _camera = null;
        _toggle = null;
        _holder = null;
        _screen = null;
        _material = null;
        _texture = null;
    }

    private void Build(PanelPlacement panel)
    {
        var prefab = CrewScreens.PrefabOf(Aircraft, out var cockpit);
        if (prefab == null || cockpit == null)
        {
            throw new InvalidOperationException($"{Aircraft.definition.jsonKey} has no tactical screen prefab");
        }

        var source = CrewScreens.ScreenRendererOf(Aircraft);
        if (source == null)
        {
            throw new InvalidOperationException($"{Aircraft.definition.jsonKey} has no screen renderer");
        }

        var targetCam = Aircraft.targetCam;
        if (targetCam == null)
        {
            throw new InvalidOperationException("the aircraft has no target camera on this client");
        }

        _source = source;
        _camera = CrewmateCam.Create(Aircraft);

        _holder = new GameObject("NoMulticrew.CrewmateScreen");
        _holder.SetActive(false);
        _holder.transform.SetParent(cockpit.transform, false);
        _holder.transform.localPosition = new Vector3(0f, -50f, 0f);

        var instance = Object.Instantiate(prefab, _holder.transform);

        _screen = instance.GetComponent<TacScreen>();
        if (_screen == null)
        {
            throw new InvalidOperationException("the tactical screen prefab has no TacScreen");
        }

        Strip<MFDAppManager>(instance);
        Strip<PylonIndicator>(instance);
        Strip<EngineTelemetry>(instance);
        Strip<MissileWarningLight>(instance);

        if (_camera?.Texture != null)
        {
            foreach (var display in new[] { TargetDisplayRef(_screen), LandingDisplayRef(_screen) })
            {
                if (display == null)
                {
                    continue;
                }

                foreach (var image in display.GetComponentsInChildren<RawImage>(true))
                {
                    image.texture = _camera.Texture;
                }
            }
        }

        foreach (var camera in instance.GetComponentsInChildren<Camera>(true))
        {
            if (camera.targetTexture == null)
            {
                continue;
            }

            _texture ??= new RenderTexture(camera.targetTexture.descriptor) { name = "NoMulticrew.CrewmateScreen" };
            camera.targetTexture = _texture;
        }

        if (_texture == null)
        {
            throw new InvalidOperationException("the tactical screen has no screen camera");
        }

        var shared = CrewScreens.MaterialRef(_screen);
        if (shared == null)
        {
            throw new InvalidOperationException("the tactical screen has no screen material");
        }

        _material = new Material(shared) { name = "NoMulticrew.CrewmateScreen" };

        _material.SetTexture(CrewScreens.EmissionMap, _texture);
        CrewScreens.MaterialRef(_screen) = _material;

        _overlay = Overlay(panel, source, _material);
        _overlay.Show(false);

        _holder.SetActive(true);
        _screen.Initialize(Aircraft, cockpit);
        CrewScreens.SyncRadar(_screen, Aircraft);

        _toggle = AccessTools.MethodDelegate<Action<TargetCam.OnCamToggle>>(CrewScreens.CamToggle, _screen);
        targetCam.onCamToggle -= _toggle;
    }

    private ScreenQuad Overlay(PanelPlacement panel, Renderer source, Material material)
    {
        var mirror = _seat == Role.Wso;

        Vector3 Side(Vector3 point)
        {
            return mirror ? new Vector3(-point.x, point.y, point.z) : point;
        }

        Vector3[] corners = mirror
            ? [Side(panel.TopRight), Side(panel.TopLeft), Side(panel.BottomLeft), Side(panel.BottomRight)]
            : [panel.TopLeft, panel.TopRight, panel.BottomRight, panel.BottomLeft];

        var normal = Side(panel.Normal).normalized;
        var cockpit = Aircraft.cockpit.transform;
        var parent = source.transform;

        for (var i = 0; i < corners.Length; i++)
        {
            corners[i] = parent.InverseTransformPoint(cockpit.TransformPoint(corners[i] + normal * PanelLift));
        }

        var topLeft = panel.UvTopLeft;
        var bottomRight = panel.UvBottomRight;

        return new ScreenQuad(
            "NoMulticrew.CrewmatePanel",
            parent,
            source.gameObject.layer,
            material,
            corners,
            [
                topLeft,
                new Vector2(bottomRight.x, topLeft.y),
                bottomRight,
                new Vector2(topLeft.x, bottomRight.y),
            ],
            parent.InverseTransformDirection(cockpit.TransformDirection(normal))
        );
    }

    private static void Strip<T>(GameObject instance)
        where T : Component
    {
        foreach (var component in instance.GetComponentsInChildren<T>(true))
        {
            Object.DestroyImmediate(component);
        }
    }
}
