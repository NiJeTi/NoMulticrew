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
        if (server == null || server.Crew.AircraftOf(__instance) is not { } seatedIn)
        {
            return;
        }

        server.Economy.Settle(__instance, seatedIn, forfeit: true);
        server.Crew.Release(__instance);
        server.Notify(__instance, "Left the seat");
    }
}
