using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace NoMulticrew.Screens;

internal sealed class CrewmateCam : IDisposable
{
    private const float Timeout = 3f;
    private const float WidenBelow = 2.5f;
    private const float WidenRate = 2f;
    private const float StartFov = 10f;
    private const float MinFov = 0.25f;
    private const float MaxFov = 20f;
    private const float FovPerSize = 75f;
    private const float RearAngle = 135f;
    private const float InfraredRange = 10000f;
    private const float RetargetDistance = 50f;

    private static readonly AccessTools.FieldRef<TargetCam, UnitPart?> AttachedPartRef =
        AccessTools.FieldRefAccess<TargetCam, UnitPart?>("attachedPart");

    private static readonly AccessTools.FieldRef<TargetCam, Transform?> ForwardMountRef =
        AccessTools.FieldRefAccess<TargetCam, Transform?>("camMountForward");

    private static readonly AccessTools.FieldRef<TargetCam, Transform?> RearMountRef =
        AccessTools.FieldRefAccess<TargetCam, Transform?>("camMountRear");

    private readonly Aircraft _aircraft;

    private GameObject? _root;
    private Camera? _camera;
    private Volume? _volume;
    private ColorAdjustments? _adjustments;
    private Transform? _part;

    private Vector3 _forwardMount;
    private Vector3 _rearMount;
    private Vector3 _target;
    private Vector3 _previous;

    private float _targetFov = 1f;
    private float _timeout;
    private float _timeOnTarget;
    private bool _infrared;

    public RenderTexture? Texture { get; private set; }

    private CrewmateCam(Aircraft aircraft)
    {
        _aircraft = aircraft;
    }

    public static CrewmateCam? Create(Aircraft aircraft)
    {
        var cam = new CrewmateCam(aircraft);

        try
        {
            cam.Build();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Failed to build the crewmate's target camera: {e}");
            cam.Dispose();

            if (cam.Texture != null)
            {
                Object.Destroy(cam.Texture);
            }

            return null;
        }

        return cam;
    }

    public bool Tick(IReadOnlyList<Unit> targets)
    {
        if (_camera == null || _part == null)
        {
            return false;
        }

        var delta = Time.deltaTime;

        if (targets.Count > 0)
        {
            Acquire(targets, delta);
        }

        if (!_camera.enabled)
        {
            return false;
        }

        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFov, delta);

        if (_timeout < WidenBelow)
        {
            _camera.fieldOfView = Mathf.Min(_camera.fieldOfView + WidenRate * delta, MaxFov);
        }

        if (_timeout <= 0f)
        {
            Switch(false);
            return false;
        }

        var rear = Vector3.Angle(_aircraft.transform.forward, _target - _aircraft.transform.position) > RearAngle;
        var mount = _part.TransformPoint(rear ? _rearMount : _forwardMount);
        var view = _camera.transform;

        view.position = mount;

        if ((_target - mount).sqrMagnitude > 1f)
        {
            var look = Quaternion.LookRotation(_target - mount, Vector3.up);
            view.rotation = _timeOnTarget < 1f ? Quaternion.Slerp(view.rotation, look, _timeOnTarget) : look;
        }

        _timeout -= delta;

        return true;
    }

    public void Dispose()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        if (_camera != null)
        {
            _camera.enabled = false;
        }

        if (_volume != null && _volume.HasInstantiatedProfile())
        {
            Object.Destroy(_volume.profile);
        }

        if (_root != null)
        {
            Object.Destroy(_root);
        }

        _root = null;
        _camera = null;
        _volume = null;
        _adjustments = null;
    }

    private void Build()
    {
        var source = _aircraft.GetComponentInChildren<TargetCam>(true);
        if (source == null)
        {
            throw new InvalidOperationException($"{_aircraft.definition.jsonKey} has no target camera");
        }

        var part = AttachedPartRef(source);
        var forward = ForwardMountRef(source);
        var rear = RearMountRef(source);
        if (part == null || forward == null || rear == null)
        {
            throw new InvalidOperationException("the target camera has no part or mounts");
        }

        _part = part.transform;
        _forwardMount = _part.InverseTransformPoint(forward.position);
        _rearMount = _part.InverseTransformPoint(rear.position);

        _root = Object.Instantiate(GameAssets.i.targetCam, _part);
        _root.name = "NoMulticrew.CrewmateTargetCam";

        var cameras = _root.GetComponentsInChildren<Camera>(true);
        if (cameras.Length == 0)
        {
            throw new InvalidOperationException("the target camera prefab has no camera");
        }

        foreach (var camera in cameras)
        {
            camera.enabled = false;
        }

        _camera = cameras[0];

        var shared = _camera.targetTexture;
        if (shared == null)
        {
            throw new InvalidOperationException("the target camera has no render texture");
        }

        Texture = new RenderTexture(shared.descriptor) { name = "NoMulticrew.CrewmateTargetView" };
        _camera.targetTexture = Texture;

        _volume = _camera.GetComponentInChildren<Volume>(true);
        IsolateVolume();

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void IsolateVolume()
    {
        if (_volume == null)
        {
            return;
        }

        _volume.enabled = false;

        var layer = FreeLayer();
        if (layer < 0)
        {
            Plugin.Logger.LogWarning("No free layer for the crewmate's target view grading; it is shown ungraded");
            Object.Destroy(_volume);
            _volume = null;

            return;
        }

        _volume.gameObject.layer = layer;
        _camera!.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << layer;

        _adjustments = _volume.profile.TryGet<ColorAdjustments>(out var adjustments) ? adjustments : null;

        Plugin.Logger.LogDebug($"Crewmate target view grading on layer {layer}");
    }

    private static int FreeLayer()
    {
        for (var layer = 31; layer >= 8; layer--)
        {
            if (LayerMask.LayerToName(layer).Length > 0)
            {
                continue;
            }

            var mask = 1 << layer;

            if (Camera.allCameras.Any(
                    x => x.TryGetComponent<UniversalAdditionalCameraData>(out var data)
                        && (data.volumeLayerMask & mask) != 0
                ))
            {
                continue;
            }

            return layer;
        }

        return -1;
    }

    private void Acquire(IReadOnlyList<Unit> targets, float delta)
    {
        var camera = _camera!;

        PositionAndSize(targets, out var position, out var size);

        if (!camera.enabled)
        {
            camera.fieldOfView = StartFov;
            camera.nearClipPlane = 2f;
            camera.farClipPlane = 60000f;
            camera.transform.SetPositionAndRotation(_part!.TransformPoint(_forwardMount), _aircraft.transform.rotation);

            _timeOnTarget = 0f;
            _previous = position;

            Switch(true);
        }

        _timeout = Timeout;

        var distance = Vector3.Distance(position, camera.transform.position);
        var level = NetworkSceneSingleton<LevelInfo>.i;

        SetInfrared(
            level.timeOfDay < 6f || level.timeOfDay > 18f || distance > InfraredRange || PlayerSettings.tacScreenIR
        );

        _targetFov = Mathf.Clamp(size * FovPerSize / distance, MinFov, MaxFov);

        _timeOnTarget += delta;
        if (Vector3.Distance(position, _previous) > RetargetDistance)
        {
            _timeOnTarget = 0f;
        }

        _previous = position;
        _target = position;

        foreach (var target in targets)
        {
            target.displayDetail = 1f;
        }
    }

    private void PositionAndSize(IReadOnlyList<Unit> targets, out Vector3 position, out float size)
    {
        if (targets.Count == 1)
        {
            position = PositionOf(targets[0]);
            size = targets[0].definition.length;

            return;
        }

        var min = Vector3.one * float.MaxValue;
        var max = -min;

        foreach (var target in targets)
        {
            var point = PositionOf(target);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        position = (min + max) * 0.5f;
        size = Mathf.Max(Mathf.Max(max.x - min.x, max.z - min.z), max.y - min.y) * 0.75f;
    }

    private Vector3 PositionOf(Unit target)
    {
        return _aircraft.NetworkHQ.GetTrackingData(target.persistentID)?.GetPosition().ToLocalPosition()
            ?? target.transform.position;
    }

    private void Switch(bool on)
    {
        _camera!.enabled = on;

        if (_volume != null)
        {
            _volume.enabled = on;
        }
    }

    private void SetInfrared(bool infrared)
    {
        _infrared = infrared;

        if (_adjustments != null)
        {
            _adjustments.saturation.overrideState = infrared;
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _camera)
        {
            RenderSettings.fog = !_infrared;
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _camera)
        {
            RenderSettings.fog = true;
        }
    }
}
