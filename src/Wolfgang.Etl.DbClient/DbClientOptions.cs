namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Process-wide knobs for <c>Wolfgang.Etl.DbClient</c>. Settings here are read
/// from the global Dapper type-map delegate that <c>ColumnAttributeTypeMapper</c>
/// installs, so they affect read-side column resolution only.
/// </summary>
/// <remarks>
/// Scope today:
/// <list type="bullet">
///   <item><description>
///   Applies to <see cref="DbExtractor{TRecord}"/> — its static constructor
///   triggers the type-map registration that consults these settings.
///   </description></item>
///   <item><description>
///   Only takes effect for <c>TRecord</c> types that have at least one
///   <see cref="System.ComponentModel.DataAnnotations.Schema.ColumnAttribute"/>;
///   types without any <c>[Column]</c> use Dapper's built-in name matcher,
///   which is not routed through this options class.
///   </description></item>
///   <item><description>
///   Does <b>not</b> affect <see cref="DbLoader{TRecord}"/>: loaders emit
///   parameters from a POCO, they don't read result-set columns.
///   </description></item>
/// </list>
/// Set the flags once at process startup, before constructing the first
/// extractor for a given <c>TRecord</c>, for the most predictable behavior.
/// </remarks>
public static class DbClientOptions
{
    /// <summary>
    /// Controls how the library reacts when a SELECT result-set column does not
    /// map to any property on the destination <c>TRecord</c> type.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///     <see langword="false"/> (default) — silently ignore the unmapped column,
    ///     matching Dapper's out-of-the-box behavior. Common with <c>SELECT *</c>
    ///     and joins that include columns the POCO doesn't need.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     <see langword="true"/> — throw an <see cref="System.InvalidOperationException"/>
    ///     naming the offending column and the target type. Useful while developing
    ///     to catch typos in <c>[Column]</c> attributes the moment they happen.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para>
    /// The flag is read on every column lookup, so flipping it at runtime takes
    /// effect for the next query without re-registering type maps.
    /// </para>
    /// </remarks>
    public static bool StrictColumnMapping { get; set; }
}
