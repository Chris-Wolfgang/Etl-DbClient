using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Validates that a record type's <c>[Table]</c> and <c>[Column]</c>
/// attribute mappings match the actual database schema before you invest
/// in an extract or load. Catches configuration errors — table renamed,
/// column dropped, <c>[Column("wrong_name")]</c> typo — at the top of a
/// batch instead of mid-operation.
/// </summary>
/// <remarks>
/// <para>
/// The validator is intentionally provider-agnostic. It executes
/// <c>SELECT * FROM &lt;table&gt; WHERE 1 = 0</c> against the supplied
/// <see cref="DbConnection"/> and inspects the resulting reader's column
/// schema. Every provider that supports ADO.NET supports this — SQL
/// Server, PostgreSQL, MySQL / MariaDB, SQLite, Oracle, CockroachDB —
/// and there's no <c>INFORMATION_SCHEMA</c> dialect drift to work around.
/// </para>
/// <para>
/// The zero-row filter is by-design: no rows come back, only metadata.
/// Even against a large production table the round-trip is a millisecond.
/// </para>
/// <para>
/// Type-compatibility checking (mapping a <see cref="string"/> property to
/// an <c>INT</c> column, for example) is not implemented today — ADO.NET
/// type mappings vary enough per provider that a best-effort check would
/// surface more false positives than real bugs. This validator focuses on
/// the two problems that <em>are</em> reliably diagnosable across
/// providers: the table is missing, or a mapped column name is not on
/// the table.
/// </para>
/// </remarks>
public static class DbSchemaValidator
{
    /// <summary>
    /// Synchronously validate that <typeparamref name="TRecord"/>'s
    /// <c>[Table]</c> exists on the connected database and every mapped
    /// column (property with no <c>[NotMapped]</c>) is present on the
    /// table.
    /// </summary>
    /// <typeparam name="TRecord">The mapped record type.</typeparam>
    /// <param name="connection">
    /// An open or closed <see cref="DbConnection"/>. Opened by the
    /// validator if closed; state is restored on return.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connection"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TRecord"/> has no <c>[Table]</c> attribute, the
    /// table does not exist on the database, or one or more mapped column
    /// names are not present on the table. The exception message names
    /// the specific offender.
    /// </exception>
    public static void Validate<TRecord>(DbConnection connection)
        where TRecord : notnull
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        ExtractMapping(typeof(TRecord), out var table, out var mappedColumns);
        var openedHere = EnsureOpen(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table} WHERE 1 = 0";
            cmd.CommandType = CommandType.Text;
            using var reader = TryExecuteReader(cmd, table);
            AssertColumnsPresent(reader, mappedColumns, table);
        }
        finally
        {
            if (openedHere)
            {
                connection.Close();
            }
        }
    }

    /// <summary>
    /// Async companion to <see cref="Validate{TRecord}"/>. Recommended for
    /// server contexts where blocking the calling thread on the round-trip
    /// is undesirable.
    /// </summary>
    /// <typeparam name="TRecord">The mapped record type.</typeparam>
    /// <param name="connection">
    /// An open or closed <see cref="DbConnection"/>. Opened by the
    /// validator if closed; state is restored on return.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="OperationCanceledException"/>
    public static async Task ValidateAsync<TRecord>(
        DbConnection connection,
        CancellationToken cancellationToken = default)
        where TRecord : notnull
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        ExtractMapping(typeof(TRecord), out var table, out var mappedColumns);
        var openedHere = await EnsureOpenAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {table} WHERE 1 = 0";
            cmd.CommandType = CommandType.Text;
            DbDataReader reader;
            try
            {
                reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbException ex)
            {
                throw new InvalidOperationException(
                    $"Schema validation failed for table '{table}': the table does not exist or cannot be queried. " +
                    $"Underlying provider error: {ex.Message}",
                    ex);
            }
            using (reader)
            {
                AssertColumnsPresent(reader, mappedColumns, table);
            }
        }
        finally
        {
            if (openedHere)
            {
#if NET5_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }

    // ------------------------------------------------------------------

    private static void ExtractMapping(Type type, out string table, out IReadOnlyList<string> columns)
    {
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        if (tableAttr is null || string.IsNullOrWhiteSpace(tableAttr.Name))
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' does not have a [Table] attribute. " +
                "Schema validation requires an explicit table mapping.");
        }

        var mapped = new List<string>();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<NotMappedAttribute>() is not null)
            {
                continue;
            }
            var columnName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
            mapped.Add(columnName);
        }

        if (mapped.Count == 0)
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' declares no mapped columns. " +
                "Every property is [NotMapped]; nothing to validate.");
        }

        table = tableAttr.Name;
        columns = mapped;
    }

    private static bool EnsureOpen(DbConnection connection)
    {
        if (connection.State == ConnectionState.Open)
        {
            return false;
        }
        connection.Open();
        return true;
    }

    private static async Task<bool> EnsureOpenAsync(DbConnection connection, CancellationToken ct)
    {
        if (connection.State == ConnectionState.Open)
        {
            return false;
        }
        await connection.OpenAsync(ct).ConfigureAwait(false);
        return true;
    }

    private static DbDataReader TryExecuteReader(DbCommand cmd, string table)
    {
        try
        {
            return cmd.ExecuteReader();
        }
        catch (DbException ex)
        {
            throw new InvalidOperationException(
                $"Schema validation failed for table '{table}': the table does not exist or cannot be queried. " +
                $"Underlying provider error: {ex.Message}",
                ex);
        }
    }

    private static void AssertColumnsPresent(
        DbDataReader reader,
        IReadOnlyList<string> mappedColumns,
        string table)
    {
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            actual.Add(reader.GetName(i));
        }

        var missing = mappedColumns.Where(c => !actual.Contains(c)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Schema validation failed for table '{table}': " +
                $"the following mapped column(s) are missing from the database: " +
                string.Join(", ", missing.Select(m => $"'{m}'")) + ". " +
                $"Table has: {string.Join(", ", actual.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}.");
        }
    }
}
