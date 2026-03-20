namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Specifies the type of SQL command the <see cref="DbLoader{TRecord,TProgress}"/>
/// auto-generates from attribute metadata.
/// </summary>
public enum WriteMode
{
    /// <summary>
    /// Generate an INSERT statement. All non-identity-key columns are included.
    /// </summary>
    Insert,

    /// <summary>
    /// Generate an UPDATE statement. Requires at least one property decorated with
    /// <c>[Key]</c> for the WHERE clause.
    /// </summary>
    Update
}
