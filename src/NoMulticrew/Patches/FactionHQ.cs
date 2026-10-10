using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NuclearOption.Networking;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(FactionHQ), nameof(FactionHQ.RewardPlayer))]
internal static class FactionHQ_RewardPlayer
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(
        FactionHQ __instance,
        Player player,
        Unit target,
        float rewardAllocation,
        float rewardScore,
        FactionHQ.RewardType missionType
    )
    {
        var economy = Plugin.Server?.Economy;

        return economy == null
            || !economy.Reward(__instance, player, target, rewardAllocation, rewardScore, missionType);
    }
}