#if NETFRAMEWORK || NETSTANDARD2_0 || NETSTANDARD2_1 || NET5_0 || NET6_0 || NET7_0
// Polyfill for record struct / init-only setters on TFMs that don't ship this type.
// The C# compiler requires System.Runtime.CompilerServices.IsExternalInit to lower
// init / record members; adding it as an internal stub here is the standard fix.

using System.ComponentModel;

// This type MUST live in System.Runtime.CompilerServices for the C# compiler
// to recognize it as the init-setter marker, regardless of the folder layout
// under the project root. The `disable once` comment must sit directly above
// the namespace line, or InspectCode does not apply it.

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
#endif
