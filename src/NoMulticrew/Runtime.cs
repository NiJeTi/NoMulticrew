// ReSharper disable CheckNamespace
// ReSharper disable UnusedType.Global

using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
internal sealed class RequiredMemberAttribute : Attribute;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public string FeatureName { get; } = featureName;
}