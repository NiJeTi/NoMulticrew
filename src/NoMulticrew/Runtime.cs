// ReSharper disable CheckNamespace
// ReSharper disable UnusedType.Global

using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    internal sealed class RequiredMemberAttribute : Attribute;

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public string FeatureName { get; } = featureName;
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullAttribute(params string[] members) : Attribute
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public string[] Members { get; } = members;
    }
}