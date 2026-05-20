namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Process-wide knobs for <c>Wolfgang.Etl.DbClient</c>. These settings flip the
/// behavior of the global Dapper type map and therefore affect every
/// <see cref="DbExtractor{TRecord}"/> / <see cref="DbLoader{TRecord}"/> instance
/// in the AppDomain — set them once at startup, before constructing extractors
/// or loaders.
/// </summary>
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
