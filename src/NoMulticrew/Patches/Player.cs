using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NuclearOption.Networking;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Player), nameof(Player.SetAircraft))]
internal static class Player_SetAircraft
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Player __instance)
    {
        var server = Plugin.Server;
        if (server?.Crew.AircraftOf(__instance) is not { } aircraftId)
        {
            return;
        }

        Plugin.Logger.LogWarning(
            $"{__instance.GetDisplayName(PlayerNameContext.Other)} got an aircraft while seated in {aircraftId}"
        );

        server.Crew.Release(__instance, forfeit: true);
    }
}