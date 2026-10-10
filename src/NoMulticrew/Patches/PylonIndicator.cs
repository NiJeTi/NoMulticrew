using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(PylonIndicator), nameof(PylonIndicator.Start))]
internal static class PylonIndicator_Start
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Transpilers.ReplaceCall(
            instructions,
            AccessTools.Method(typeof(GameManager), nameof(GameManager.GetLocalAircraft)),
            AccessTools.Method(typeof(PylonIndicator_Start), nameof(GetAircraft)),
            "PylonIndicator.Start"
        );
    }

    private static bool GetAircraft(out Aircraft aircraft)
    {
        if (GameManager.GetLocalAircraft(out aircraft))
        {
            return true;
        }

        var hud = SceneSingleton<CombatHUD>.i;
        aircraft = hud != null ? hud.aircraft : null!;

        return aircraft != null;
    }
}