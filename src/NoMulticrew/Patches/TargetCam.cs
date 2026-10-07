using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Reflection;
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
        return Replace(
            instructions,
            AccessTools.PropertyGetter(typeof(NetworkIdentity), nameof(NetworkIdentity.HasAuthority)),
            AccessTools.Method(typeof(TargetCam_Initialize), nameof(HasAuthority)),
            "TargetCam.Initialize"
        );
    }

    public static IEnumerable<CodeInstruction> Replace(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo getter,
        MethodInfo helper,
        string where
    )
    {
        var replaced = 0;

        foreach (var instruction in instructions)
        {
            if (instruction.Calls(getter))
            {
                replaced++;
                yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = helper };
                continue;
            }

            yield return instruction;
        }

        if (replaced != 1)
        {
            Plugin.Logger.LogError($"{where}: expected one {getter.Name} call, replaced {replaced}");
        }
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
            || Plugin.SeatTable.SeatsFor(___aircraft.definition.jsonKey).Count == 0)
        {
            return true;
        }

        __instance.enabled = false;
        return false;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return TargetCam_Initialize.Replace(
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
