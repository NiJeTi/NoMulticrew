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

    private static readonly AccessTools.FieldRef<MapIcon, bool> IsSelectedRef =
        AccessTools.FieldRefAccess<MapIcon, bool>("isSelected");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(UnitMapIcon __instance, ref Color __result)
    {
        var marks = Plugin.Client?.Marks;
        if (marks == null || !marks.TryGetColor(__instance.unit, out var color))
        {
            return;
        }

        if (IsSelectedRef(__instance))
        {
            var alpha = color.a;
            color = Color.Lerp(color, Color.white, SelectedBrighten);
            color.a = alpha;
        }

        color.a *= __result.a;
        __result = color;
    }
}
