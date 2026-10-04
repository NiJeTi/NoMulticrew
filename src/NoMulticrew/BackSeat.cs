using Cysharp.Threading.Tasks;
using HarmonyLib;
using NoMulticrew.Crew;
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

    private readonly CrewState _crew;

    private readonly HashSet<TargetDetector> _scanning = [];

    private Transform? _originalViewPoint;
    private GameObject? _rearViewPoint;

    public Aircraft? Aircraft { get; private set; }

    public int SeatIndex { get; private set; } = -1;

    public BackSeat(CrewState crew)
    {
        _crew = crew;
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

    public void Leave(bool showMap)
    {
        var aircraft = Aircraft;
        if (aircraft is null)
        {
            return;
        }

        Aircraft = null;
        SeatIndex = -1;

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
                + $"{seats.Count}. This client's Seats.json differs from the server's."
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
        _rearViewPoint.transform.localPosition =
            cockpit.InverseTransformPoint(original.position) + seats[seatIndex].ViewOffset;
        _rearViewPoint.transform.rotation = original.rotation;

        aircraft.cockpitViewPoint = _rearViewPoint.transform;

        SceneSingleton<CombatHUD>.i.SetAircraft(aircraft);

        var camera = SceneSingleton<CameraStateManager>.i;
        camera.SetFollowingUnit(aircraft);
        camera.SwitchState(camera.cockpitState);

        SceneSingleton<DynamicMap>.i.Minimize();

        FlightHud.EnableCanvas(true);

        StartScanLoops(aircraft);

        Plugin.Logger.LogInfo(
            $"Entered seat {seatIndex} of {aircraft.definition.jsonKey} at {seats[seatIndex].ViewOffset:F2}"
        );
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

        Plugin.Logger.LogInfo($"Started {started} scan loops");
    }
}