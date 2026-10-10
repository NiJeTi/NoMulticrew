using System.Reflection;
using HarmonyLib;

namespace NoMulticrew;

internal static class GameMembers
{
    public static MethodInfo Method(Type type, string name, Type[]? parameters = null)
    {
        return AccessTools.Method(type, name, parameters) ?? throw new MissingMethodException(type.Name, name);
    }

    public static FieldInfo Field(Type type, string name)
    {
        return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.Name, name);
    }
}