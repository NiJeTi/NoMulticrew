using HarmonyLib;
using NoMulticrew.Seats;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoMulticrew.Screens;

internal sealed class CrewmateScreen : IDisposable
{
    private const float PanelLift = 0.002f;

    private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private static readonly Color Tint = new(1f, 0f, 1f);

    private GameObject? _holder;
    private TacScreen? _screen;
    private RenderTexture? _texture;
    private Material? _material;
    private Renderer? _source;
    private ScreenQuad? _overlay;

    public Aircraft Aircraft { get; }

    public int Seat { get; }

    private CrewmateScreen(Aircraft aircraft, int seat)
    {
        Aircraft = aircraft;
        Seat = seat;
    }

    public static CrewmateScreen? Create(Aircraft aircraft, int seat, PanelPlacement panel)
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

    public void Tick()
    {
        _overlay?.Show(_source != null && _source.enabled);
    }

    public void Dispose()
    {
        _overlay?.Dispose();
        _overlay = null;

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
        if (targetCam == null || CrewScreens.CamToggle == null)
        {
            throw new InvalidOperationException("the aircraft has no target camera on this client");
        }

        _source = source;

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

        if (Plugin.Settings.TintCrewmatePanel.Value)
        {
            _material.SetTexture(EmissionMap, null);
            _material.SetColor(EmissionColor, Tint);
            _material.SetColor(BaseColor, Tint);
        }
        else
        {
            _material.SetTexture(EmissionMap, _texture);
            CrewScreens.MaterialRef(_screen) = _material;
        }

        _overlay = Overlay(panel, source, _material);
        _overlay.Show(false);

        _holder.SetActive(true);
        _screen.Initialize(Aircraft, cockpit);

        targetCam.onCamToggle -=
            AccessTools.MethodDelegate<Action<TargetCam.OnCamToggle>>(CrewScreens.CamToggle, _screen);
    }

    private ScreenQuad Overlay(PanelPlacement panel, Renderer source, Material material)
    {
        var mirror = Seat == SeatTable.Wso;

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
