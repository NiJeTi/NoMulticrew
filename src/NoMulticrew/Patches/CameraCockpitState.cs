using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(CameraCockpitState), nameof(CameraCockpitState.UpdateState))]
internal static class CameraCockpitState_UpdateState
{
    private const float ReportSeconds = 2f;

    private static readonly int[] Frames = new int[3];
    private static readonly float[] ErrorSum = new float[3];
    private static readonly float[] ErrorMax = new float[3];

    private static int _lastFrame = -1;
    private static GlobalPosition _lastView;
    private static Vector3 _lastVelocity;
    private static float _lastFixedTime;
    private static float _travelSum;
    private static int _readBeforeSnapshot;
    private static float _driftMax;
    private static float _reportedAt;

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Aircraft? ___aircraft)
    {
        var aircraft = ___aircraft;
        if (aircraft == null || Plugin.Client is not { } client || !ReferenceEquals(client.BackSeat.Aircraft, aircraft))
        {
            return;
        }

        var rb = aircraft.CockpitRB();
        var viewPoint = aircraft.cockpitViewPoint;
        if (rb == null || viewPoint == null)
        {
            return;
        }

        var frame = Time.frameCount;
        var view = viewPoint.GlobalPosition();
        var velocity = rb.velocity;

        if (frame == _lastFrame + 1 && Time.deltaTime > 0f)
        {
            var steps = Mathf.Clamp(Mathf.RoundToInt((Time.fixedTime - _lastFixedTime) / Time.fixedDeltaTime), 0, 2);
            var expected = 0.5f * (velocity + _lastVelocity) * Time.deltaTime;
            var error = (view - _lastView - expected).magnitude;

            Frames[steps]++;
            ErrorSum[steps] += error;
            ErrorMax[steps] = Mathf.Max(ErrorMax[steps], error);
            _travelSum += expected.magnitude;

            if (AircraftNetworkTransform_ApplySnapshot.Applied is { } applied && applied.Frame == frame)
            {
                _driftMax = Mathf.Max(_driftMax, (aircraft.transform.position - applied.Position).magnitude);
            }
            else
            {
                _readBeforeSnapshot++;
            }
        }

        _lastFrame = frame;
        _lastView = view;
        _lastVelocity = velocity;
        _lastFixedTime = Time.fixedTime;

        if (Time.unscaledTime - _reportedAt < ReportSeconds)
        {
            return;
        }

        _reportedAt = Time.unscaledTime;

        var total = Frames[0] + Frames[1] + Frames[2];
        if (total > 0)
        {
            Plugin.Logger.LogInfo(
                $"Back-seat ride: {total} frames, speed {velocity.magnitude:F0} m/s, "
                + $"travel {_travelSum / total:F3} m/frame, one fixed step {velocity.magnitude * Time.fixedDeltaTime:F3} m; "
                + $"0 steps: {Bucket(0)}; 1 step: {Bucket(1)}; 2+ steps: {Bucket(2)}; "
                + $"read before snapshot: {_readBeforeSnapshot}, drift after snapshot: max {_driftMax:F3} m"
            );
        }

        Array.Clear(Frames, 0, Frames.Length);
        Array.Clear(ErrorSum, 0, ErrorSum.Length);
        Array.Clear(ErrorMax, 0, ErrorMax.Length);
        _travelSum = 0f;
        _readBeforeSnapshot = 0;
        _driftMax = 0f;
    }

    private static string Bucket(int steps)
    {
        return Frames[steps] == 0
            ? "no frames"
            : $"{Frames[steps]} frames, error mean {ErrorSum[steps] / Frames[steps]:F3} m, max {ErrorMax[steps]:F3} m";
    }
}
