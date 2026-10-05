using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(AircraftSelectionMenu), nameof(AircraftSelectionMenu.Refresh))]
internal static class AircraftSelectionMenu_Refresh
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Airbase airbase)
    {
        Plugin.Client?.SeatList.Refresh(airbase);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(AircraftSelectionMenu), "HideSelection")]
internal static class AircraftSelectionMenu_HideSelection
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix()
    {
        Plugin.Client?.SeatList.Clear();
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(VirtualMFD), "Start")]
internal static class VirtualMFD_Start
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(VirtualMFD __instance)
    {
        Plugin.Client?.AttachMfd(__instance);
    }
}
