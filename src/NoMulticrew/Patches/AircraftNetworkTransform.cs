using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using NuclearOption.NetworkTransforms;
using UnityEngine;

namespace NoMulticrew.Patches;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[HarmonyPatch(typeof(AircraftNetworkTransform), "ApplySnapshot")]
internal static class AircraftNetworkTransform_ApplySnapshot
{
    public static (int Frame, Vector3 Position, Quaternion Rotation)? Applied { get; private set; }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(AircraftNetworkTransform __instance, ref NetworkTransformBase.ViewSnapshot snapshot)
    {
        if (Plugin.Client is { } client && ReferenceEquals(client.BackSeat.Aircraft, __instance.Aircraft))
        {
            Applied = (Time.frameCount, snapshot.Position, snapshot.Rotation);
        }
    }
}
