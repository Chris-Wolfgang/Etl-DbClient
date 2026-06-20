using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Generates SQL commands from <see cref="TableAttribute"/>, <see cref="ColumnAttribute"/>,
/// and <see cref="KeyAttribute"/> annotations on a POCO type.
/// </summary>
/// <remarks>
/// Reflection results are cached per <see cref="Type"/> via <see cref="TypeMetadataCache"/>;
/// the cached entry contains the table name, column mappings, key set, and the
/// already-built SELECT / INSERT / UPDATE strings. The first Build* call for a given
/// type pays the reflection cost; subsequent calls return cached strings.
/// </remarks>
internal static class DbCommandBuilder
{
    /// <summary>
    /// Generates a SELECT statement for all mapped columns.
    /// Requires <see cref="TableAttribute"/> on the type.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type does not have a <see cref="TableAttribute"/>.
    /// </exception>
    internal static string BuildSelect<T>() => TypeMetadataCache.For(typeof(T)).SelectSql;



    /// <summary>
    /// Generates an INSERT statement for all mapped non-key columns.
    /// If no <see cref="KeyAttribute"/> columns exist, all columns are included.
    /// Requires <see cref="TableAttribute"/> on the type.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type does not have a <see cref="TableAttribute"/> or has no
    /// mapped columns for INSERT.
    /// </exception>
    internal static string BuildInsert<T>() => TypeMetadataCache.For(typeof(T)).InsertSql;



    /// <summary>
    /// Generates an UPDATE statement using <see cref="KeyAttribute"/> columns in the WHERE clause.
    /// Requires <see cref="TableAttribute"/> and at least one <see cref="KeyAttribute"/> property.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type does not have a <see cref="TableAttribute"/>, no <see cref="KeyAttribute"/>
    /// properties, or no non-key columns for the SET clause.
    /// </exception>
    internal static string BuildUpdate<T>() => TypeMetadataCache.For(typeof(T)).UpdateSql;



    // ------------------------------------------------------------------
    // Internal cache: one entry per Type. Reflection runs once.
    // ------------------------------------------------------------------

    /// <summary>
    /// Per-type cache of reflection metadata and pre-built SQL strings.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/>.
    /// </summary>
    private static class TypeMetadataCache
    {
        private static readonly ConcurrentDictionary<Type, TypeMetadata> Cache = new();

        internal static TypeMetadata For(Type type) => Cache.GetOrAdd(type, BuildMetadata);
    }



    private sealed class TypeMetadata
    {
        public TypeMetadata(string selectSql, Lazy<string> insertSql, Lazy<string> updateSql)
        {
            SelectSql = selectSql;
            _insertSql = insertSql;
            _updateSql = updateSql;
        }

        // SELECT can never throw on a type with at least one mapped property (or
        // returns "SELECT *" if there are none) so it's safe to build eagerly.
        public string SelectSql { get; }

        // INSERT / UPDATE may throw on validation failures (no non-key columns,
        // [Key]-but-[NotMapped], etc). Defer until first request so a type that
        // only ever calls BuildSelect doesn't surface those errors at cache time.
        private readonly Lazy<string> _insertSql;
        public string InsertSql => _insertSql.Value;

        private readonly Lazy<string> _updateSql;
        public string UpdateSql => _updateSql.Value;
    }



    // ------------------------------------------------------------------
    // Metadata construction (runs once per Type)
    // ------------------------------------------------------------------

    // S3398 wants BuildMetadata moved inside TypeMetadataCache since that's
    // its only caller. Declining — BuildMetadata depends on the file-private
    // helpers below (BuildSelectSql, BuildInsertSql, BuildUpdateSql,
    // GetTableName, ColumnMapping), and nesting all of them inside
    // TypeMetadataCache to satisfy a single style rule hurts readability
    // more than it helps. The current shape — cache as a small nested type,
    // build logic at the outer class — keeps each concern visible.
#pragma warning disable S3398
    private static TypeMetadata BuildMetadata(Type type)
    {
        var table = GetTableName(type);
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Single pass: read [NotMapped], [Column], [Key], [DatabaseGenerated]
        // off each property and bucket the results.
        // - `columns`           — mapped columns (excludes [NotMapped]).
        // - `anyKeyPropertyNames` — every property with [Key], including those
        //   that also have [NotMapped]. BuildUpdate needs this to distinguish
        //   "no [Key] anywhere" from "[Key] exists but all are [NotMapped]".
        // - `autoIdentityKeyPropertyNames` — subset that is BOTH [Key] AND
        //   [DatabaseGenerated(Identity)]; excluded from INSERT.
        var columns = new List<ColumnMapping>(capacity: props.Length);
        var anyKeyPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        var autoIdentityKeyPropertyNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var prop in props)
        {
            var isNotMapped = prop.GetCustomAttribute<NotMappedAttribute>() != null;
            var isKey = prop.GetCustomAttribute<KeyAttribute>() != null;

            if (isKey)
            {
                anyKeyPropertyNames.Add(prop.Name);
            }

            if (isNotMapped)
            {
                continue;
            }

            var columnName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
            columns.Add(new ColumnMapping(columnName, prop.Name));

            if (isKey)
            {
                var dbGenerated = prop.GetCustomAttribute<DatabaseGeneratedAttribute>();
                if (dbGenerated?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                {
                    autoIdentityKeyPropertyNames.Add(prop.Name);
                }
            }
        }

        return new TypeMetadata
        (
            selectSql: BuildSelectSql(table, columns),
            // BuildInsert / BuildUpdate validate column counts and may throw. Wrap in
            // Lazy so a type that only ever calls BuildSelect (e.g. one with all
            // [NotMapped] properties) doesn't surface the INSERT failure at cache time.
            insertSql: new Lazy<string>(() => BuildInsertSql(type, table, columns, autoIdentityKeyPropertyNames)),
            updateSql: new Lazy<string>(() => BuildUpdateSql(type, table, columns, anyKeyPropertyNames))
        );
    }
#pragma warning restore S3398



    private static string BuildSelectSql(string table, List<ColumnMapping> columns)
    {
        if (columns.Count == 0)
        {
            return $"SELECT * FROM {table}";
        }

        var columnList = string.Join
        (
            ", ",
            columns.Select(c => string.Equals(c.ColumnName, c.PropertyName, StringComparison.Ordinal)
                ? c.ColumnName
                : $"{c.ColumnName} AS {c.PropertyName}")
        );

        return $"SELECT {columnList} FROM {table}";
    }



    private static string BuildInsertSql
    (
        Type type,
        string table,
        List<ColumnMapping> columns,
        HashSet<string> autoIdentityKeyPropertyNames
    )
    {
        // Exclude [Key] + [DatabaseGenerated(Identity)] columns from INSERT.
        // Non-identity keys (e.g., composite keys) are included since they're
        // user-assigned values.
        List<ColumnMapping> insertColumns = autoIdentityKeyPropertyNames.Count > 0
            ? columns.Where(c => !autoIdentityKeyPropertyNames.Contains(c.PropertyName)).ToList()
            : columns;

        if (insertColumns.Count == 0)
        {
            insertColumns = columns;
        }

        if (insertColumns.Count == 0)
        {
            throw new InvalidOperationException
            (
                $"Type '{type.FullName}' has no mapped columns for INSERT. " +
                "Ensure at least one public instance property is not decorated with [NotMapped]."
            );
        }

        var columnList = string.Join(", ", insertColumns.Select(c => c.ColumnName));
        var paramList = string.Join(", ", insertColumns.Select(c => $"@{c.PropertyName}"));

        return $"INSERT INTO {table} ({columnList}) VALUES ({paramList})";
    }



    private static string BuildUpdateSql
    (
        Type type,
        string table,
        List<ColumnMapping> columns,
        HashSet<string> keyPropertyNames
    )
    {
        if (keyPropertyNames.Count == 0)
        {
            throw new InvalidOperationException
            (
                $"Type '{type.FullName}' has no properties decorated with [Key]. " +
                "UPDATE requires at least one key property for the WHERE clause."
            );
        }

        if (columns.Count == 0)
        {
            throw new InvalidOperationException
            (
                $"Type '{type.FullName}' has no mapped columns. " +
                "UPDATE requires at least one mapped property that is not decorated with [NotMapped]."
            );
        }

        var whereColumns = columns.Where(c => keyPropertyNames.Contains(c.PropertyName)).ToList();
        var setColumns = columns.Where(c => !keyPropertyNames.Contains(c.PropertyName)).ToList();

        if (whereColumns.Count == 0)
        {
            throw new InvalidOperationException
            (
                $"Type '{type.FullName}' has [Key] properties but none are mapped columns. " +
                "Ensure key properties are not decorated with [NotMapped]."
            );
        }

        if (setColumns.Count == 0)
        {
            throw new InvalidOperationException
            (
                $"Type '{type.FullName}' has no non-key columns for the SET clause. " +
                "UPDATE requires at least one property that is not decorated with [Key]."
            );
        }

        var setClause = string.Join(", ", setColumns.Select(c => $"{c.ColumnName} = @{c.PropertyName}"));
        var whereClause = string.Join(" AND ", whereColumns.Select(c => $"{c.ColumnName} = @{c.PropertyName}"));

        return $"UPDATE {table} SET {setClause} WHERE {whereClause}";
    }



    // ------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------

    private static string GetTableName(Type type)
    {
        var attr = type.GetCustomAttribute<TableAttribute>();
        if (attr == null)
        {
            throw new InvalidOperationException
            (
                $"Type '{type.FullName}' does not have a [Table] attribute. " +
                "Auto-generated SQL requires [Table(\"name\")] on the record type, " +
                "or pass a custom command text to the constructor."
            );
        }

        return attr.Name;
    }



    private readonly record struct ColumnMapping(string ColumnName, string PropertyName);
}
