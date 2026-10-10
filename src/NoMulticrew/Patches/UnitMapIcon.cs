using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(UnitMapIcon), "GetColor")]
[HarmonyAfter("NoWingmen")]
internal static class UnitMapIcon_GetColor
{
    private const float SelectedBrighten = 0.35f;

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(UnitMapIcon __instance, bool ___isSelected, ref Color __result)
    {
        var marks = Plugin.Client?.Marks;
        if (marks == null || !marks.TryGetColor(__instance.unit, out var color))
        {
            return;
        }

        if (___isSelected)
        {
            var alpha = color.a;
            color = Color.Lerp(color, Color.white, SelectedBrighten);
            color.a = alpha;
        }

        color.a *= __result.a;
        __result = color;
    }
}