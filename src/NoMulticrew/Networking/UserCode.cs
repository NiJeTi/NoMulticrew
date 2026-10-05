using System.Reflection;
using HarmonyLib;

namespace NoMulticrew.Networking;

internal static class UserCode
{
    public static MethodInfo? Find(Type type, string name)
    {
        var prefix = $"UserCode_{name}_";

        return AccessTools.GetDeclaredMethods(type)
            .FirstOrDefault(x => x.Name.StartsWith(prefix, StringComparison.Ordinal));
    }
}
