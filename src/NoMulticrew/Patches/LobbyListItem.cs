using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NoMulticrew.Networking;
using NuclearOption.Networking.Lobbies;
using TMPro;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(LobbyListItem), nameof(LobbyListItem.Show))]
internal static class LobbyListItem_Show
{
    private const string Badge = "  <color=#07D8A8>[CREW]</color>";

    private static readonly AccessTools.FieldRef<LobbyListItem, TextMeshProUGUI> LobbyNameTextRef =
        AccessTools.FieldRefAccess<LobbyListItem, TextMeshProUGUI>("lobbyNameText");

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(LobbyListItem __instance, LobbyInstance lobby, bool __result)
    {
        if (!__result || lobby == null || !Plugin.Settings.ShowCapableServerBadge.Value)
        {
            return;
        }

        if (!Discovery.IsCapable(lobby))
        {
            return;
        }

        var label = LobbyNameTextRef(__instance);

        if (label == null || label.text.Contains("[CREW]"))
        {
            return;
        }

        label.text += Badge;
    }
}
