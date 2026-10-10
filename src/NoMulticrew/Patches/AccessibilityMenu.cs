using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Client.Theming;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(AccessibilityMenu), "Awake")]
[HarmonyAfter("NoWingmen")]
internal static class AccessibilityMenu_Awake
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(AccessibilityMenu __instance)
    {
        MarkPaletteSection.Build(__instance);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(AccessibilityMenu), "LoadColorPickers")]
internal static class AccessibilityMenu_LoadColorPickers
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(AccessibilityMenu __instance)
    {
        MarkPaletteSection.Refresh(__instance);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(AccessibilityMenu), "OnThemeGroupSave")]
internal static class AccessibilityMenu_OnThemeGroupSave
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix()
    {
        MarkPaletteSection.Commit();
    }
}