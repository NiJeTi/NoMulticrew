using HarmonyLib;
using NuclearOption.Networking;

namespace NoMulticrew.Networking;

internal static class NetworkManager
{
    private static readonly AccessTools.FieldRef<ResourcesAsyncLoader<NetworkManagerNuclearOption>>
        NetworkManagerLoaderRef = AccessTools.StaticFieldRefAccess<ResourcesAsyncLoader<NetworkManagerNuclearOption>>(
            AccessTools.Field(typeof(NetworkManagerNuclearOption), "loader")
        );

    public static NetworkManagerNuclearOption? Instance =>
        NetworkManagerLoaderRef().IsLoaded ? NetworkManagerNuclearOption.i : null;
}
