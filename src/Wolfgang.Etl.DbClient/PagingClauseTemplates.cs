namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Ready-made values for <see cref="DbExtractorOptions.PagingClauseTemplate"/>, one per supported
/// database.
/// </summary>
/// <remarks>
/// Server-side paging syntax is <b>dialect-specific</b>, not standard SQL. <c>LIMIT … OFFSET …</c>
/// is the PostgreSQL / MySQL / SQLite form; SQL Server rejects <c>LIMIT</c> outright and wants the
/// SQL:2008 <c>OFFSET … ROWS FETCH NEXT … ROWS ONLY</c> form, as do Oracle 12c+ and Db2.
/// <para>
/// These are conveniences, not a closed set — <see cref="DbExtractorOptions.PagingClauseTemplate"/>
/// takes any string, so a dialect with no preset here is supplied directly.
/// </para>
/// <para>
/// Every template must reference both <c>@PageOffset</c> and <c>@PageLimit</c>: those are the
/// parameter names the extractor supplies when <see cref="DbExtractorOptions.ServerOffset"/> and
/// <see cref="DbExtractorOptions.ServerLimit"/> are set.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new DbExtractorOptions
/// {
///     ServerOffset = 100,
///     ServerLimit = 50,
///     PagingClauseTemplate = PagingClauseTemplates.SqlServer
/// };
/// </code>
/// </example>
public static class PagingClauseTemplates
{
    private const string LimitOffset = "LIMIT @PageLimit OFFSET @PageOffset";

    private const string OffsetFetch = "OFFSET @PageOffset ROWS FETCH NEXT @PageLimit ROWS ONLY";


    /// <summary>
    /// No dialect chosen. This is the default, and it is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// This does NOT disable paging — paging is controlled by <c>ServerOffset</c> and
    /// <c>ServerLimit</c>, and applies only when both are set. <see cref="None"/> means the
    /// dialect has not been named yet, so activating paging while it is in effect is an error
    /// rather than a silent guess: there is no portable paging syntax, and any default this
    /// library picked would be wrong on half the engines it supports.
    /// </remarks>
    public static string? None => null;



    /// <summary>MySQL and MariaDB — <c>LIMIT … OFFSET …</c>.</summary>
    public static string MySql { get; } = LimitOffset;


    /// <summary>PostgreSQL — <c>LIMIT … OFFSET …</c>.</summary>
    public static string PostgreSql { get; } = LimitOffset;


    /// <summary>SQLite — <c>LIMIT … OFFSET …</c>. This is also the library default.</summary>
    public static string Sqlite { get; } = LimitOffset;


    /// <summary>SQL Server 2012 and later — SQL:2008 <c>OFFSET … FETCH NEXT …</c>.</summary>
    public static string SqlServer { get; } = OffsetFetch;


    /// <summary>Oracle 12c and later — SQL:2008 <c>OFFSET … FETCH NEXT …</c>.</summary>
    public static string Oracle { get; } = OffsetFetch;


    /// <summary>Db2 — SQL:2008 <c>OFFSET … FETCH NEXT …</c>.</summary>
    public static string Db2 { get; } = OffsetFetch;
}
