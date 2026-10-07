using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Mirage;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(TargetCam), nameof(TargetCam.Initialize))]
internal static class TargetCam_Initialize
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Transpilers.ReplaceCall(
            instructions,
            AccessTools.PropertyGetter(typeof(NetworkIdentity), nameof(NetworkIdentity.HasAuthority)),
            AccessTools.Method(typeof(TargetCam_Initialize), nameof(HasAuthority)),
            "TargetCam.Initialize"
        );
    }

    private static bool HasAuthority(NetworkIdentity identity)
    {
        return identity.HasAuthority || Plugin.Client?.BackSeat.TargetCam.Initializing == true;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(TargetCam), "Update")]
internal static class TargetCam_Update
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(TargetCam __instance, Aircraft? ___aircraft)
    {
        var client = Plugin.Client;

        if (client == null
            || ___aircraft == null
            || ReferenceEquals(client.BackSeat.Aircraft, ___aircraft)
            || (___aircraft.Player != null && ___aircraft.Player.IsLocalPlayer)
            || Plugin.SeatTable.WsoSeat(___aircraft) == null)
        {
            return true;
        }

        __instance.enabled = false;
        return false;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Transpilers.ReplaceCall(
            instructions,
            AccessTools.PropertyGetter(typeof(NetworkBehaviour), nameof(NetworkBehaviour.IsLocalPlayer)),
            AccessTools.Method(typeof(TargetCam_Update), nameof(IsLocalPlayer)),
            "TargetCam.Update"
        );
    }

    private static bool IsLocalPlayer(NetworkBehaviour player)
    {
        var seated = Plugin.Client?.BackSeat.Aircraft;

        return player.IsLocalPlayer || (seated != null && ReferenceEquals(seated.Player, player));
    }
}
