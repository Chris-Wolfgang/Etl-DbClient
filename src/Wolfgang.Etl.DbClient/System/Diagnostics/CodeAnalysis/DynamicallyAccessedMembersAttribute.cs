#if NETFRAMEWORK || NETSTANDARD2_0
// Polyfill for the trimming annotation on TFMs that don't ship it (it arrived in .NET 5).
// The trimmer and the trim/AOT analyzers match it by full name, so an internal copy on the
// older TFMs lets the same source carry the annotation everywhere; only the net8.0+ assets
// are ever trimmed or AOT-compiled, and those use the BCL type.

// ReSharper disable once CheckNamespace -- the trimmer recognizes this attribute only in
// System.Diagnostics.CodeAnalysis, regardless of the folder layout under the project root.
namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage
    (
        AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter |
        AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method |
        AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
        Inherited = false
    )]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
        {
            MemberTypes = memberTypes;
        }



        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }
}
#endif
