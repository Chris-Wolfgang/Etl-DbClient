// Polyfill for netstandard2.0 — records use init-only setters, which the C#
// compiler emits as `[modreq(IsExternalInit)]`. The type exists in
// netstandard2.1+ / net5.0+; we declare it ourselves for older targets.
//
// The C# compiler recognises this type by fully-qualified name, so the
// namespace MUST be System.Runtime.CompilerServices even though the file
// lives under Wolfgang.Etl.DbClient.SourceGenerator. Suppress InspectCode's
// CheckNamespace on this one file only.
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
