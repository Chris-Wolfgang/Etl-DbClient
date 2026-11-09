using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// The members of a record type that the extractor and loader read through reflection:
/// public constructors (row materialization) and public properties (column mapping,
/// parameter binding, SQL generation). Applied as
/// <c>[DynamicallyAccessedMembers]</c> to every <c>TRecord</c> so the
/// trimmer keeps those members and the trim/AOT analyzers can verify the reflection.
/// </summary>
internal static class RecordMembers
{
    internal const DynamicallyAccessedMemberTypes Mapped =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties;
}
