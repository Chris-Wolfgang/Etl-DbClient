#if NETFRAMEWORK || NETSTANDARD2_0 || NETSTANDARD2_1 || NETCOREAPP3_1 || NET5_0 || NET6_0 || NET7_0
// Polyfill for init-only setters on TFMs that don't ship this type.
// The C# compiler requires System.Runtime.CompilerServices.IsExternalInit to
// lower `init` setters; adding it as an internal stub here is the standard fix.

using System.ComponentModel;

// This type MUST live in System.Runtime.CompilerServices for the C# compiler
// to recognize it as the init-setter marker, regardless of the folder layout
// under the project root — the `disable once` below documents that contract.

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
#endif
