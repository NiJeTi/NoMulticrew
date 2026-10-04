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