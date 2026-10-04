using HarmonyLib;
using NuclearOption.Networking;

namespace NoMulticrew.Networking;

internal static class NetworkManagerProvider
{
    private static readonly AccessTools.FieldRef<ResourcesAsyncLoader<NetworkManagerNuclearOption>>
        NetworkManagerLoaderRef = AccessTools.StaticFieldRefAccess<ResourcesAsyncLoader<NetworkManagerNuclearOption>>(
            AccessTools.Field(typeof(NetworkManagerNuclearOption), "loader")
        );

    public static NetworkManagerNuclearOption? Current =>
        NetworkManagerLoaderRef().IsLoaded ? NetworkManagerNuclearOption.i : null;
}