using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(CameraCockpitState), nameof(CameraCockpitState.FixedUpdateState))]
internal static class CameraCockpitState_FixedUpdateState
{
    private static readonly AccessTools.FieldRef<Aircraft?> FeedbackAircraftRef = FeedbackRef<Aircraft?>("_aircraft");
    private static readonly AccessTools.FieldRef<AudioSource?> SourceRef = FeedbackRef<AudioSource?>("_source");

    private static readonly AccessTools.FieldRef<AircraftParameters.OnboardAoAEffects?> AoAEffectsRef =
        FeedbackRef<AircraftParameters.OnboardAoAEffects?>("aoaEffects");

    private static readonly AccessTools.FieldRef<float> VolumeRef = FeedbackRef<float>("volume");
    private static readonly AccessTools.FieldRef<float> VolumeSmoothedRef = FeedbackRef<float>("volumeSmoothed");
    private static readonly AccessTools.FieldRef<float> ShakeRef = FeedbackRef<float>("shake");
    private static readonly AccessTools.FieldRef<float> ShakeSmoothedRef = FeedbackRef<float>("shakeSmoothed");
    private static readonly AccessTools.FieldRef<float> LastUpdateRef = FeedbackRef<float>("lastUpdate");

    private static readonly Action<Aircraft> SetupAircraft = AccessTools.MethodDelegate<Action<Aircraft>>(
        GameMembers.Method(typeof(AoAFeedback), "SetupAircraft")
    );

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(
        CameraStateManager cam,
        Aircraft? ___aircraft,
        ref Vector3 ___velocityPrev,
        ref Vector3 ___accel,
        ref Vector3 ___accelPrev,
        ref float ___gForce,
        ref float ___gForcePrev,
        ref float ___jerk,
        ref Vector3 ___camRelativePos,
        ref Vector3 ___camRelativeVel,
        ref float ___antiSlump,
        ref float ___lowFreqShake,
        ref float ___highFreqShake
    )
    {
        var aircraft = ___aircraft;
        if (aircraft == null || Plugin.Client is not { } client || !ReferenceEquals(client.BackSeat.Aircraft, aircraft))
        {
            return true;
        }

        ___gForce = 0f;
        ___jerk = 0f;

        var rb = aircraft.CockpitRB();
        if (rb == null || !(Time.deltaTime > 0f))
        {
            return false;
        }

        var velocity = client.BackSeat.SmoothVelocity(rb.velocity, ___velocityPrev == Vector3.zero);
        ___accel = ___velocityPrev == Vector3.zero ? Vector3.zero : (velocity - ___velocityPrev) / Time.deltaTime;
        var spring = -500f * ___camRelativePos.magnitude * ___camRelativePos.normalized;
        var slump = Vector3.Dot(cam.transform.up, -___camRelativePos);
        ___antiSlump += slump * 1000f * Time.deltaTime;
        spring += cam.transform.up * ___antiSlump;
        ___camRelativeVel += (-Vector3.ClampMagnitude(___accel, 500f) + spring) * Time.deltaTime;
        ___camRelativeVel -= Vector3.ClampMagnitude(
            ___camRelativeVel * 20f * Time.deltaTime, ___camRelativeVel.magnitude
        );
        ___gForce = ___accel.magnitude / 9.81f;
        ___jerk = ___gForcePrev == 0f ? 0f : (___gForce - ___gForcePrev) / Time.deltaTime;
        ___velocityPrev = velocity;
        ___gForcePrev = ___gForce;
        ___accelPrev = ___accel;
        ___lowFreqShake = Mathf.Clamp(___jerk * 0.005f, ___lowFreqShake, 1f);
        ___lowFreqShake = Mathf.Lerp(___lowFreqShake, 0f, 5f * Time.fixedDeltaTime);
        ___highFreqShake = Mathf.Lerp(___highFreqShake, 0f, 4f * Time.fixedDeltaTime);
        cam.cockpitRattle.volume = ___lowFreqShake;

        if (!cam.cockpitRattle.isPlaying)
        {
            if (___lowFreqShake > 0f)
            {
                cam.cockpitRattle.Play();
            }
        }
        else if (___lowFreqShake == 0f)
        {
            cam.cockpitRattle.Stop();
        }

        RunAoAFeedback(aircraft, velocity);

        return false;
    }

    private static void RunAoAFeedback(Aircraft aircraft, Vector3 velocity)
    {
        if (FeedbackAircraftRef() != aircraft)
        {
            SetupAircraft(aircraft);
        }

        ref var volume = ref VolumeRef();
        ref var volumeSmoothed = ref VolumeSmoothedRef();
        ref var shake = ref ShakeRef();
        ref var shakeSmoothed = ref ShakeSmoothedRef();
        ref var lastUpdate = ref LastUpdateRef();

        if (Time.timeSinceLevelLoad - lastUpdate < 0.1f)
        {
            volumeSmoothed = Mathf.Lerp(volumeSmoothed, volume, 8f * Time.fixedDeltaTime);
            shakeSmoothed = Mathf.Lerp(shakeSmoothed, shake, 8f * Time.fixedDeltaTime);
            SourceRef()!.volume = volumeSmoothed;
            SceneSingleton<CameraStateManager>.i.ShakeCamera(0f, shakeSmoothed);
        }
        else
        {
            lastUpdate = Time.timeSinceLevelLoad;
            var aoaEffects = AoAEffectsRef()!;
            var direction = velocity -
                NetworkSceneSingleton<LevelInfo>.i.GetWind(aircraft.cockpit.xform.GlobalPosition());
            var local = aircraft.cockpit.xform.InverseTransformDirection(direction);
            var alpha = Mathf.Atan2(local.y, local.z) * 57.29578f;
            var speedFactor = Mathf.Max(aircraft.speed - aoaEffects.OnsetSpeed, 0f)
                / (aoaEffects.FullVolumeSpeed - aoaEffects.OnsetSpeed);
            var alphaFactor = Mathf.Max(Mathf.Abs(alpha) - aoaEffects.OnsetAlpha, 0f)
                / (aoaEffects.FullVolumeAlpha - aoaEffects.OnsetAlpha);
            volume = Mathf.Sqrt(speedFactor * alphaFactor);
            shake = aoaEffects.ShakeFactor * speedFactor * alphaFactor;
        }
    }

    private static AccessTools.FieldRef<T> FeedbackRef<T>(string name)
    {
        return AccessTools.StaticFieldRefAccess<T>(GameMembers.Field(typeof(AoAFeedback), name));
    }
}