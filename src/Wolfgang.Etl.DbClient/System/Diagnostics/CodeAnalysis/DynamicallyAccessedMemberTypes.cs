#if NETFRAMEWORK || NETSTANDARD2_0
// Polyfill for the trimming annotation on TFMs that don't ship it (it arrived in .NET 5).
// The trimmer and the trim/AOT analyzers match it by full name, so an internal copy on the
// older TFMs lets the same source carry the annotation everywhere; only the net8.0+ assets
// are ever trimmed or AOT-compiled, and those use the BCL type.

// ReSharper disable once CheckNamespace -- the trimmer recognizes this attribute only in
// System.Diagnostics.CodeAnalysis, regardless of the folder layout under the project root.
namespace System.Diagnostics.CodeAnalysis
{
    // S4070 / MA0062 see a non-flags enum because PublicConstructors is 0x0002 | PublicParameterlessConstructor
    // with no named 0x0002 member. That is exactly how the BCL declares it, and the values must mirror the
    // BCL so the annotation means the same thing on every TFM.
#pragma warning disable S4070, MA0062
    [Flags]
    internal enum DynamicallyAccessedMemberTypes
    {
        None = 0,
        PublicParameterlessConstructor = 0x0001,
        PublicConstructors = 0x0002 | PublicParameterlessConstructor,
        PublicProperties = 0x0200,
    }
#pragma warning restore S4070, MA0062
}
#endif
