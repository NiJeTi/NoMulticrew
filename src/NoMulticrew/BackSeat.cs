using Cysharp.Threading.Tasks;
using HarmonyLib;
using NoMulticrew.Crew;
using NoMulticrew.Networking;
using NuclearOption.Networking;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NoMulticrew;

internal sealed class BackSeat : IDisposable
{
    private static readonly System.Reflection.MethodInfo? HideSelectionMethod =
        AccessTools.Method(typeof(AircraftSelectionMenu), "HideSelection");

    private static readonly System.Reflection.MethodInfo? RepeatSearchMethod =
        AccessTools.Method(typeof(TargetDetector), "RepeatSearch");

    private static readonly AccessTools.FieldRef<Weapon, Hardpoint?> HardpointRef =
        AccessTools.FieldRefAccess<Weapon, Hardpoint?>("hardpoint");

    private readonly CrewState _crew;
    private readonly Controls _controls;

    private readonly HashSet<TargetDetector> _scanning = [];

    private Transform? _originalViewPoint;
    private GameObject? _rearViewPoint;

    private CameraStateManager? _camera;

    private Vector3 _viewBase;

    public Aircraft? Aircraft { get; private set; }

    public BackSeatWeapons Weapons { get; }

    public int SeatIndex { get; private set; } = -1;

    public int Station { get; private set; } = -1;

    public BackSeat(ClientSession session, Controls controls)
    {
        _crew = session.Crew;
        _controls = controls;
        Weapons = new BackSeatWeapons(session, this);
    }

    public void Dispose()
    {
        Leave(showMap: false);
    }

    public void Tick()
    {
        _crew.TryGetLocalSeat(out var aircraft, out var seatIndex);

        if (ReferenceEquals(aircraft, Aircraft) && seatIndex == SeatIndex)
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

        Enter(aircraft, seatIndex);
    }

    private void Employ()
    {
        if (Aircraft == null)
        {
            return;
        }

        if (!Owns(Station))
        {
            Select(Next(Station, 1));
        }

        if (_controls.IsNextWeaponPressed())
        {
            Select(Next(Station, 1));
        }

        if (_controls.IsPreviousWeaponPressed())
        {
            Select(Next(Station, -1));
        }

        Weapons.Tick(_controls.IsFireHeld());
    }

    private void Select(int station)
    {
        if (station == Station || Aircraft == null)
        {
            return;
        }

        Station = station;

        Plugin.Logger.LogDebug($"Back seat selected station {station}");

        SceneSingleton<CombatHUD>.i.ShowWeaponStation(station >= 0 ? Aircraft.weaponStations[station] : null);

        MoveView();
    }

    private void MoveView()
    {
        if (Aircraft == null || _rearViewPoint == null)
        {
            return;
        }

        var seats = Plugin.SeatTable.SeatsFor(Aircraft.definition.jsonKey);
        if (SeatIndex < 0 || SeatIndex >= seats.Count)
        {
            return;
        }

        var seat = seats[SeatIndex];
        var view = Station >= 0 ? seat.ViewFor(SetsOf(Aircraft, Aircraft.weaponStations[Station])) : seat.DefaultView;

        _rearViewPoint.transform.localPosition = _viewBase + view.Offset;

        Plugin.Logger.LogDebug($"Back seat view for station {Station}: {view}");
    }

    private static HashSet<string> SetsOf(Aircraft aircraft, WeaponStation station)
    {
        var names = new HashSet<string>();

        foreach (var weapon in station.Weapons)
        {
            var hardpoint = weapon != null ? HardpointRef(weapon) : null;
            if (hardpoint == null)
            {
                continue;
            }

            foreach (var set in aircraft.weaponManager.hardpointSets)
            {
                if (set.hardpoints.Contains(hardpoint))
                {
                    names.Add(set.name);
                }
            }
        }

        return names;
    }

    private int Next(int from, int direction)
    {
        var count = Aircraft!.weaponStations.Count;

        for (var step = 1; step <= count; step++)
        {
            var candidate = ((from + direction * step) % count + count) % count;
            if (Owns(candidate))
            {
                return candidate;
            }
        }

        return -1;
    }

    public bool Owns(Unit unit, int station)
    {
        return Aircraft != null && ReferenceEquals(unit, Aircraft) && CrewState.Owns(Aircraft, SeatIndex, station);
    }

    private bool Owns(int station)
    {
        return Aircraft != null && Owns(Aircraft, station);
    }

    private void Reattach()
    {
        if (Aircraft == null || _rearViewPoint == null)
        {
            return;
        }

        var camera = SceneSingleton<CameraStateManager>.i;
        if (camera == null || camera.followingUnit != null || CameraStateManager.cameraMode == CameraMode.selection)
        {
            return;
        }

        camera.SetFollowingUnit(Aircraft);
        camera.SwitchState(camera.cockpitState);

        FlightHud.EnableCanvas(true);
    }

    private void OnSwitchCamera()
    {
        var camera = _camera;
        if (Aircraft == null
            || camera == null
            || camera.currentState != camera.cockpitState
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
        SeatIndex = -1;
        Station = -1;
        Weapons.Clear();

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

    private void Enter(Aircraft aircraft, int seatIndex)
    {
        Aircraft = aircraft;
        SeatIndex = seatIndex;

        var seats = Plugin.SeatTable.SeatsFor(aircraft.definition.jsonKey);
        if (seatIndex >= seats.Count)
        {
            Plugin.Logger.LogError(
                $"Seated in seat {seatIndex} of {aircraft.definition.jsonKey}, but the local seat table has "
                + $"{seats.Count}. This client's seat table differs from the server's."
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
        _rearViewPoint.transform.SetParent(cockpit, false);
        _viewBase = cockpit.InverseTransformPoint(original.position);
        _rearViewPoint.transform.localPosition = _viewBase + seats[seatIndex].DefaultView.Offset;
        _rearViewPoint.transform.rotation = original.rotation;

        aircraft.cockpitViewPoint = _rearViewPoint.transform;

        SceneSingleton<CombatHUD>.i.SetAircraft(aircraft);

        var camera = SceneSingleton<CameraStateManager>.i;
        _camera = camera;
        camera.onSwitchCamera += OnSwitchCamera;
        camera.SetFollowingUnit(aircraft);
        camera.SwitchState(camera.cockpitState);

        SceneSingleton<DynamicMap>.i.Minimize();

        FlightHud.EnableCanvas(true);

        StartScanLoops(aircraft);

        Plugin.Logger.LogInfo($"Entered seat {seatIndex} of {aircraft.definition.jsonKey}: {seats[seatIndex]}");
    }

    private static bool CloseDeployMenu()
    {
        var menu = Object.FindObjectOfType<AircraftSelectionMenu>();
        if (menu == null)
        {
            return false;
        }

        if (HideSelectionMethod == null)
        {
            Plugin.Logger.LogError("AircraftSelectionMenu.HideSelection not found; the deploy menu stays open");

            return false;
        }

        HideSelectionMethod.Invoke(menu, []);

        return true;
    }

    private void StartScanLoops(Aircraft aircraft)
    {
        if (Plugin.IsServer)
        {
            return;
        }

        if (RepeatSearchMethod == null)
        {
            Plugin.Logger.LogError(
                "TargetDetector.RepeatSearch not found; the back seat sees only contacts the faction already knows"
            );

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

            if (RepeatSearchMethod.Invoke(detector, []) is UniTask task)
            {
                task.Forget();
                started++;
            }
        }

        Plugin.Logger.LogDebug($"Started {started} scan loops");
    }
}