using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(Unit), nameof(Unit.NetworkremoteWeaponStates), MethodType.Setter)]
internal static class Unit_SetNetworkremoteWeaponStates
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(Unit __instance, ref WeaponMask value)
    {
        Plugin.Server?.Commands.MergeFiring(__instance, ref value);
    }
}
