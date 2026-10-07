using Cysharp.Threading.Tasks;
using HarmonyLib;
using NoMulticrew.Networking;
using NoMulticrew.Seats;
using NuclearOption.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NoMulticrew;

internal sealed class BackSeat : IDisposable
{
    private static readonly Action<AircraftSelectionMenu> HideSelection =
        AccessTools.MethodDelegate<Action<AircraftSelectionMenu>>(
            GameMembers.Method(typeof(AircraftSelectionMenu), "HideSelection")
        );

    private static readonly Func<TargetDetector, UniTask> RepeatSearch =
        AccessTools.MethodDelegate<Func<TargetDetector, UniTask>>(
            GameMembers.Method(typeof(TargetDetector), "RepeatSearch")
        );

    private static readonly AccessTools.FieldRef<CombatHUD, GameObject> CountermeasureBackgroundRef =
        AccessTools.FieldRefAccess<CombatHUD, GameObject>("countermeasureBackground");

    private static readonly AccessTools.FieldRef<CombatHUD, Image> CountermeasureImageRef =
        AccessTools.FieldRefAccess<CombatHUD, Image>("countermeasureImage");

    private static readonly AccessTools.FieldRef<CombatHUD, TextMeshProUGUI> CountermeasureNameRef =
        AccessTools.FieldRefAccess<CombatHUD, TextMeshProUGUI>("countermeasureName");

    private static readonly AccessTools.FieldRef<CombatHUD, TextMeshProUGUI> CountermeasureAmmoRef =
        AccessTools.FieldRefAccess<CombatHUD, TextMeshProUGUI>("countermeasureAmmo");

    private const float BailOutConfirmSeconds = 3f;

    private const float RideTimeConstant = 0.2f;

    private readonly ClientSession _session;

    private readonly HashSet<TargetDetector> _scanning = [];

    private Transform? _originalViewPoint;
    private GameObject? _rearViewPoint;

    private CameraStateManager? _camera;

    private float _bailOutArmedAt = float.NegativeInfinity;

    private Vector3 _rideVelocity;

    public Aircraft? Aircraft { get; private set; }

    public BackSeatWeapons Weapons { get; }

    public CrewTargetCam TargetCam { get; } = new();

    public int Station { get; private set; } = -1;

    public bool BailOutArmed =>
        Aircraft != null && Time.unscaledTime - _bailOutArmedAt <= BailOutConfirmSeconds;

    public BackSeat(ClientSession session)
    {
        _session = session;
        Weapons = new BackSeatWeapons(session, this);
    }

    public void Dispose()
    {
        Leave(showMap: false);
    }

    public void Tick()
    {
        _session.Crew.TryGetSeatedAircraft(out var aircraft);

        if (ReferenceEquals(aircraft, Aircraft))
        {
            Reattach();
            Employ();

            return;
        }

        Leave(showMap: true);

        if (aircraft == null)
        {
            return;
        }

        if (CloseDeployMenu())
        {
            return;
        }

        Enter(aircraft);
    }

    public bool RequestLeave()
    {
        if (Aircraft == null)
        {
            return false;
        }

        if (SeatTable.IsValidExit(Aircraft) || BailOutArmed)
        {
            _bailOutArmedAt = float.NegativeInfinity;
            _session.Send(new CrewLeaveRequest());
            return false;
        }

        _bailOutArmedAt = Time.unscaledTime;
        _session.Prompt.ShowNotice("Press Eject again to bail out — your sortie earnings go to the pilot");

        return true;
    }

    public void LoadoutChanged(Aircraft aircraft)
    {
        if (ReferenceEquals(aircraft, Aircraft))
        {
            Station = -1;
        }
    }

    public Vector3 SmoothVelocity(Vector3 velocity, bool restart)
    {
        _rideVelocity = restart
            ? velocity
            : Vector3.Lerp(_rideVelocity, velocity, 1f - Mathf.Exp(-Time.fixedDeltaTime / RideTimeConstant));

        return _rideVelocity;
    }

    private void Employ()
    {
        if (Aircraft == null)
        {
            return;
        }

        if (!_session.Crew.CanSelect(Aircraft, Role.Wso, Station))
        {
            if (Station >= 0 && Plugin.SeatTable.IsShared(Aircraft))
            {
                _session.Crew.RefuseStation(Aircraft, Station);
            }

            var confirmed = _session.Crew.ClientState(Aircraft).WsoStation;

            Select(_session.Crew.CanSelect(Aircraft, Role.Wso, confirmed) ? confirmed : Next(Station, 1));
        }

        if (Controls.IsNextWeaponPressed())
        {
            Select(Next(Station, 1));
        }

        if (Controls.IsPreviousWeaponPressed())
        {
            Select(Next(Station, -1));
        }

        if (Controls.IsEjectDown() && RequestLeave())
        {
            Feedback.Play(CrewCue.WeaponSwitch);
        }

        Weapons.Tick(Controls.IsFireHeld());
    }

    public void Select(int station)
    {
        if (station == Station)
        {
            return;
        }

        var aircraft = Aircraft!;

        Weapons.ReleaseAll();
        Station = station;

        _session.SendCommand(
            CrewCommand.SelectStation(aircraft.persistentID, station >= 0 ? (byte)station : SeatTable.NoStation)
        );

        if (station >= 0)
        {
            aircraft.weaponManager.currentWeaponStation = aircraft.weaponStations[station];
            Weapons.PushTargets();
        }

        Plugin.Logger.LogDebug($"Back seat selected station {station}");

        SceneSingleton<CombatHUD>.i.ShowWeaponStation(station >= 0 ? aircraft.weaponStations[station] : null);
    }

    private int Next(int from, int direction)
    {
        var aircraft = Aircraft!;
        var count = aircraft.weaponStations.Count;

        for (var step = 1; step <= count; step++)
        {
            var candidate = ((from + direction * step) % count + count) % count;
            if (_session.Crew.CanSelect(aircraft, Role.Wso, candidate))
            {
                return candidate;
            }
        }

        return -1;
    }

    public bool Owns(Unit unit, int station)
    {
        return Aircraft != null
            && ReferenceEquals(unit, Aircraft)
            && _session.Crew.RoleHolding(Aircraft, station) == Role.Wso;
    }

    private void Reattach()
    {
        if (Aircraft == null || _rearViewPoint == null)
        {
            return;
        }

        var camera = SceneSingleton<CameraStateManager>.i;
        if (camera.followingUnit != null || CameraStateManager.cameraMode == CameraMode.selection)
        {
            return;
        }

        camera.SetFollowingUnit(Aircraft);
        camera.SwitchState(camera.cockpitState);

        FlightHud.EnableCanvas(true);
    }

    private void OnSwitchCamera()
    {
        var camera = _camera!;
        if (camera.currentState != camera.cockpitState
            || !ReferenceEquals(camera.followingUnit, Aircraft)
            || DynamicMap.mapMaximized)
        {
            return;
        }

        FlightHud.EnableCanvas(true);
    }

    public void Leave(bool showMap)
    {
        var aircraft = Aircraft;
        if (aircraft is null)
        {
            return;
        }

        Aircraft = null;
        _bailOutArmedAt = float.NegativeInfinity;
        _session.Screens.Leave();
        TargetCam.Detach();
        Station = -1;
        Weapons.Clear();

        if (aircraft != null)
        {
            aircraft.weaponManager.GetTargetList().Clear();
        }

        if (aircraft != null && _originalViewPoint != null)
        {
            aircraft.cockpitViewPoint = _originalViewPoint;
        }

        if (_rearViewPoint != null)
        {
            Object.Destroy(_rearViewPoint);
        }

        _originalViewPoint = null;
        _rearViewPoint = null;

        if (_camera != null)
        {
            _camera.onSwitchCamera -= OnSwitchCamera;
            _camera = null;
        }

        var hud = SceneSingleton<CombatHUD>.i;
        if (hud != null)
        {
            ShowCountermeasures(hud, true);
        }

        if (hud != null && ReferenceEquals(hud.aircraft, aircraft))
        {
            hud.RemoveAircraft();
            FlightHud.EnableCanvas(false);
        }

        var camera = SceneSingleton<CameraStateManager>.i;
        if (camera != null && ReferenceEquals(camera.followingUnit, aircraft))
        {
            camera.SetFollowingUnit(null);
        }

        if (showMap && !GameManager.GetLocalAircraft(out _) && GameManager.GetLocalPlayer<Player>(out var player))
        {
            player.ShowMap(0f);
        }

        Plugin.Logger.LogInfo("Left the back seat");
    }

    private void Enter(Aircraft aircraft)
    {
        Aircraft = aircraft;

        aircraft.weaponManager.GetTargetList().Clear();

        var seat = Plugin.SeatTable.WsoSeat(aircraft);
        if (seat == null)
        {
            Plugin.Logger.LogError(
                $"Seated as WSO of {aircraft.definition.jsonKey}, but the local seat table has no WSO for it. "
                + "This client's seat table differs from the server's."
            );

            return;
        }

        var original = aircraft.cockpitViewPoint;
        if (original == null)
        {
            Plugin.Logger.LogError($"{aircraft.definition.jsonKey} has no cockpit view point");

            return;
        }

        _originalViewPoint = original;
        _rearViewPoint = new GameObject("NoMulticrew.RearViewPoint");
        var cockpit = aircraft.cockpit.transform;
        var eye = cockpit.InverseTransformPoint(original.position) + seat.View;
        _rearViewPoint.transform.SetParent(cockpit, false);
        _rearViewPoint.transform.localPosition = eye;
        _rearViewPoint.transform.rotation = original.rotation;

        aircraft.cockpitViewPoint = _rearViewPoint.transform;

        SceneSingleton<CombatHUD>.i.SetAircraft(aircraft);
        ShowCountermeasures(SceneSingleton<CombatHUD>.i, false);
        TargetCam.Attach(aircraft);

        var camera = SceneSingleton<CameraStateManager>.i;
        _camera = camera;
        camera.onSwitchCamera += OnSwitchCamera;
        camera.SetFollowingUnit(aircraft);
        camera.SwitchState(camera.cockpitState);

        _session.Screens.Board(aircraft, seat, _rearViewPoint.transform, eye);

        SceneSingleton<DynamicMap>.i.Minimize();

        FlightHud.EnableCanvas(true);

        StartScanLoops(aircraft);

        Plugin.Logger.LogInfo($"Entered the WSO seat of {aircraft.definition.jsonKey}: {seat}");
    }

    private static void ShowCountermeasures(CombatHUD hud, bool visible)
    {
        CountermeasureBackgroundRef(hud).SetActive(visible);
        CountermeasureImageRef(hud).enabled = visible;
        CountermeasureNameRef(hud).enabled = visible;
        CountermeasureAmmoRef(hud).enabled = visible;
    }

    private static bool CloseDeployMenu()
    {
        var menu = Object.FindObjectOfType<AircraftSelectionMenu>();
        if (menu == null)
        {
            return false;
        }

        HideSelection(menu);

        return true;
    }

    public bool Searches(TargetDetector detector)
    {
        var unit = detector.GetAttachedUnit();

        return Plugin.IsServer || GameManager.IsLocalAircraft(unit) || ReferenceEquals(unit, Aircraft);
    }

    private void StartScanLoops(Aircraft aircraft)
    {
        if (Plugin.IsServer)
        {
            return;
        }

        _scanning.RemoveWhere(detector => detector == null);

        var started = 0;

        foreach (var detector in aircraft.GetComponentsInChildren<TargetDetector>())
        {
            if (!_scanning.Add(detector))
            {
                continue;
            }

            RepeatSearch(detector).Forget();
            started++;
        }

        Plugin.Logger.LogDebug($"Started {started} scan loops");
    }
}