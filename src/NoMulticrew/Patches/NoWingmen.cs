using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace NoMulticrew.Patches;

internal static class NoWingmen
{
    private static readonly Assembly? Assembly =
        Chainloader.PluginInfos.TryGetValue("NoWingmen", out var plugin) ? plugin.Instance?.GetType().Assembly : null;

    public static bool Installed => Assembly != null;

    public static MethodInfo? Find(string type, string method, params Type[] parameters)
    {
        return Assembly?.GetType(type)
            ?.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, parameters, null);
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class TargetClaimIndex_IsClaimer
{
    private static readonly MethodBase? Target =
        NoWingmen.Find("NoWingmen.Targets.TargetClaimIndex", "IsClaimer", typeof(Aircraft));

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null && NoWingmen.Installed)
        {
            Plugin.Logger.LogWarning(
                "NoWingmen TargetClaimIndex.IsClaimer not found, patch skipped"
            );
        }

        return Target != null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodBase TargetMethod()
    {
        return Target!;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Aircraft aircraft, ref bool __result)
    {
        if (__result && Plugin.Client?.BackSeat.Aircraft is { } own && ReferenceEquals(aircraft, own))
        {
            __result = false;
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch]
internal static class MarkColorResolver_TryResolveIdentity
{
    private static readonly MethodBase? Target =
        NoWingmen.Find(
            "NoWingmen.Marks.MarkColorResolver", "TryResolveIdentity", typeof(Unit), typeof(Color).MakeByRefType()
        );

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prepare()
    {
        if (Target == null && NoWingmen.Installed)
        {
            Plugin.Logger.LogWarning(
                "NoWingmen MarkColorResolver.TryResolveIdentity not found, patch skipped"
            );
        }

        return Target != null;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static MethodBase TargetMethod()
    {
        return Target!;
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Unit unit, ref bool __result)
    {
        if (__result && Plugin.Client?.BackSeat.Aircraft is { } own && ReferenceEquals(unit, own))
        {
            __result = false;
        }
    }
}