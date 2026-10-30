#if NETFRAMEWORK || NETSTANDARD2_0
// Polyfill for the trimming annotation on TFMs that don't ship it (it arrived in .NET 5).
// The trimmer and the trim/AOT analyzers match it by full name, so an internal copy on the
// older TFMs lets the same source carry the annotation everywhere; only the net8.0+ assets
// are ever trimmed or AOT-compiled, and those use the BCL type.

// ReSharper disable once CheckNamespace -- the trimmer recognizes this attribute only in
// System.Diagnostics.CodeAnalysis, regardless of the folder layout under the project root.
namespace System.Diagnostics.CodeAnalysis
{
    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicParameterlessConstructor = 0x0001,
        PublicConstructors = 0x0002 | PublicParameterlessConstructor,
        PublicProperties = 0x0200,
    }
}
#endif
