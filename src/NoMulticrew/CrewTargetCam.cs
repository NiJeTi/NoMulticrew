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

        aircraft.OnTouchdown -= Handler<Action>(cam, "TargetCam_OnTouchdown");
        aircraft.onSetGear -= Handler<Action<Aircraft.OnSetGear>>(cam, "TargetCam_OnSetGear");

        _cam = cam;
        _aircraft = aircraft;
    }

    public void Detach()
    {
        var cam = _cam;
        var aircraft = _aircraft;

        _cam = null;
        _aircraft = null;

        if (cam == null)
        {
            return;
        }

        DestroyOrReport(CamRef(cam)?.gameObject, "camera");
        DestroyOrReport(UiCamRef(cam)?.gameObject, null);
        DestroyOrReport(TargetCanvasRef(cam), null);
        DestroyOrReport(LandingCanvasRef(cam), null);

        RenderPipelineManager.beginCameraRendering -=
            Handler<Action<ScriptableRenderContext, Camera>>(cam, "OnBeginCameraRendering");
        RenderPipelineManager.endCameraRendering -=
            Handler<Action<ScriptableRenderContext, Camera>>(cam, "OnEndCameraRendering");

        var detach = Handler<Action<UnitPart>>(cam, "TargetCam_OnDetach");
        var part = AttachedPartRef(cam);
        if (part != null)
        {
            part.onParentDetached -= detach;
        }

        if (aircraft != null)
        {
            aircraft.onDisableUnit -= Handler<Action<Unit>>(cam, "TargetCam_OnUnitDisable");

            if (aircraft.cockpit != null)
            {
                aircraft.cockpit.onParentDetached -= detach;
            }

            if (ReferenceEquals(aircraft.targetCam, cam))
            {
                aircraft.targetCam = null;
            }
        }

        cam.enabled = false;
    }

    private static T Handler<T>(TargetCam cam, string name)
        where T : Delegate
    {
        return AccessTools.MethodDelegate<T>(AccessTools.Method(typeof(TargetCam), name), cam);
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
