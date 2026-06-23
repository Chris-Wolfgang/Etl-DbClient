// Polyfill for netstandard2.0 — records use init-only setters, which the C#
// compiler emits as `[modreq(IsExternalInit)]`. The type exists in
// netstandard2.1+ / net5.0+; we declare it ourselves for older targets.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
