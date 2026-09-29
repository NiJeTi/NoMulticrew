// ReSharper disable CheckNamespace
// ReSharper disable UnusedType.Global

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class RequiredMemberAttribute : Attribute;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
{
    public string FeatureName { get; } = featureName;
}
