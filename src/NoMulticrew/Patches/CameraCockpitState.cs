using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(CameraCockpitState), nameof(CameraCockpitState.FixedUpdateState))]
internal static class CameraCockpitState_FixedUpdateState
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Transpilers.ReplaceCall(
            instructions,
            AccessTools.PropertyGetter(typeof(Rigidbody), nameof(Rigidbody.velocity)),
            AccessTools.Method(typeof(CameraCockpitState_FixedUpdateState), nameof(Velocity)),
            "CameraCockpitState.FixedUpdateState"
        );
    }

    private static Vector3 Velocity(Rigidbody rb)
    {
        return Plugin.Client?.BackSeat.RideVelocity(rb) ?? rb.velocity;
    }
}
