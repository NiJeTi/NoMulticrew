using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NoMulticrew;

internal sealed class CrewTargetCam
{
    private static readonly AccessTools.FieldRef<TargetCam, Camera?> CamRef =
        AccessTools.FieldRefAccess<TargetCam, Camera?>("cam");

    private static readonly AccessTools.FieldRef<TargetCam, Camera?> UiCamRef =
        AccessTools.FieldRefAccess<TargetCam, Camera?>("UICam");

    private static readonly AccessTools.FieldRef<TargetCam, GameObject?> TargetCanvasRef =
        AccessTools.FieldRefAccess<TargetCam, GameObject?>("canvasObjectTarget");

    private static readonly AccessTools.FieldRef<TargetCam, GameObject?> LandingCanvasRef =
        AccessTools.FieldRefAccess<TargetCam, GameObject?>("canvasObjectLanding");

    private static readonly AccessTools.FieldRef<TargetCam, UnitPart?> AttachedPartRef =
        AccessTools.FieldRefAccess<TargetCam, UnitPart?>("attachedPart");

    private static readonly MethodInfo OnTouchdown = GameMembers.Method(typeof(TargetCam), "TargetCam_OnTouchdown");

    private static readonly MethodInfo OnSetGear = GameMembers.Method(typeof(TargetCam), "TargetCam_OnSetGear");

    private static readonly MethodInfo OnBeginRendering = GameMembers.Method(typeof(TargetCam), "OnBeginCameraRendering");

    private static readonly MethodInfo OnEndRendering = GameMembers.Method(typeof(TargetCam), "OnEndCameraRendering");

    private static readonly MethodInfo OnDetach = GameMembers.Method(typeof(TargetCam), "TargetCam_OnDetach");

    private static readonly MethodInfo OnUnitDisable = GameMembers.Method(typeof(TargetCam), "TargetCam_OnUnitDisable");

    private TargetCam? _cam;
    private Aircraft? _aircraft;

    public bool Initializing { get; private set; }

    public void Attach(Aircraft aircraft)
    {
        var cam = aircraft.GetComponentInChildren<TargetCam>(true);
        if (cam == null)
        {
            Plugin.Logger.LogWarning($"{aircraft.definition.jsonKey} has no target camera on this client");
            return;
        }

        Initializing = true;

        try
        {
            cam.Initialize();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to build the crew's target camera: {e}");
        }
        finally
        {
            Initializing = false;
        }

        if (!ReferenceEquals(aircraft.targetCam, cam))
        {
            Plugin.Logger.LogError("The crew's target camera did not initialize");
            return;
        }

        aircraft.OnTouchdown -= AccessTools.MethodDelegate<Action>(OnTouchdown, cam);
        aircraft.onSetGear -= AccessTools.MethodDelegate<Action<Aircraft.OnSetGear>>(OnSetGear, cam);

        _cam = cam;
        _aircraft = aircraft;
    }

    public void Detach()
    {
        var cam = _cam;
        var aircraft = _aircraft;

        _cam = null;
        _aircraft = null;

        if (ReferenceEquals(cam, null))
        {
            return;
        }

        var camera = CamRef(cam);
        var uiCamera = UiCamRef(cam);

        DestroyOrReport(camera != null ? camera.gameObject : null, "camera");
        DestroyOrReport(uiCamera != null ? uiCamera.gameObject : null, null);
        DestroyOrReport(TargetCanvasRef(cam), null);
        DestroyOrReport(LandingCanvasRef(cam), null);

        RenderPipelineManager.beginCameraRendering -=
            AccessTools.MethodDelegate<Action<ScriptableRenderContext, Camera>>(OnBeginRendering, cam);
        RenderPipelineManager.endCameraRendering -=
            AccessTools.MethodDelegate<Action<ScriptableRenderContext, Camera>>(OnEndRendering, cam);

        var detach = AccessTools.MethodDelegate<Action<UnitPart>>(OnDetach, cam);
        var part = AttachedPartRef(cam);
        if (part != null)
        {
            part.onParentDetached -= detach;
        }

        if (aircraft != null)
        {
            aircraft.onDisableUnit -= AccessTools.MethodDelegate<Action<Unit>>(OnUnitDisable, cam);

            if (aircraft.cockpit != null)
            {
                aircraft.cockpit.onParentDetached -= detach;
            }

            if (ReferenceEquals(aircraft.targetCam, cam))
            {
                aircraft.targetCam = null;
            }
        }

        if (cam != null)
        {
            cam.enabled = false;
        }
    }

    private static void DestroyOrReport(GameObject? target, string? required)
    {
        if (target != null)
        {
            Object.Destroy(target);
            return;
        }

        if (required != null)
        {
            Plugin.Logger.LogWarning($"Target camera teardown found no {required}");
        }
    }
}
