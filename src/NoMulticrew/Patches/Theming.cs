using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Theming;
using NuclearOption.UIStyleSystem;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(ThemeManager), nameof(ThemeManager.SaveActiveThemeGroup))]
internal static class ThemeManager_SaveActiveThemeGroup
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix()
    {
        Plugin.Palette.Save(ThemeManager.Active);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(ThemeManager), nameof(ThemeManager.CopyActiveThemeGroupWithNewId))]
internal static class ThemeManager_CopyActiveThemeGroupWithNewId
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(out ThemeGroup __state)
    {
        __state = ThemeManager.Active;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(string __result, ThemeGroup __state)
    {
        Plugin.Palette.Copy(__state, __result);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(ThemeManager), nameof(ThemeManager.DeleteActiveThemeGroup))]
internal static class ThemeManager_DeleteActiveThemeGroup
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(out string __state)
    {
        __state = ThemeManager.Active.Id;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(string __state)
    {
        Plugin.Palette.Drop(__state);
    }
}

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
