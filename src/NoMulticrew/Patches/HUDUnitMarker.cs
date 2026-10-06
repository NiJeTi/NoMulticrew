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
