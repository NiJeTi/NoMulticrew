using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(HUDUnitMarker), "UpdateColor")]
[HarmonyAfter("NoWingmen")]
internal static class HUDUnitMarker_UpdateColor
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(HUDUnitMarker __instance)
    {
        var marks = Plugin.Client?.Marks;
        if (marks == null || __instance.selected || !marks.TryGetColor(__instance.unit, out var color))
        {
            return;
        }

        color.a *= __instance.image.color.a;
        __instance.image.color = color;
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(HUDUnitMarker), "SetFactionColor")]
[HarmonyAfter("NoWingmen")]
internal static class HUDUnitMarker_SetFactionColor
{
    private static readonly AccessTools.FieldRef<HUDUnitMarker, Color> ColorRef =
        AccessTools.FieldRefAccess<HUDUnitMarker, Color>("color");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(HUDUnitMarker __instance)
    {
        var marks = Plugin.Client?.Marks;
        if (marks == null || __instance.selected || !marks.TryGetColor(__instance.unit, out var color))
        {
            return;
        }

        color.a *= ColorRef(__instance).a;
        ColorRef(__instance) = color;
    }
}

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
