using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Configures Dapper to respect <see cref="ColumnAttribute"/> when mapping
/// result set columns to POCO properties. Without this, Dapper only matches
/// by property name.
/// </summary>
/// <remarks>
/// Column name matching is case-insensitive (<see cref="StringComparison.OrdinalIgnoreCase"/>),
/// consistent with Dapper's default behavior and the default collation of SQL Server, SQLite,
/// and MySQL. PostgreSQL lowercases unquoted identifiers, so case-insensitive matching is also
/// correct for standard PostgreSQL usage.
/// </remarks>
/// <remarks>
/// Reflection runs once per type at <see cref="Register{T}"/> time. The result is a
/// <see cref="Dictionary{TKey,TValue}"/> keyed by both <see cref="ColumnAttribute.Name"/>
/// and the property name (case-insensitive), so the per-column lookup that Dapper
/// performs on every row is an O(1) dictionary read with no further reflection or LINQ.
/// </remarks>
internal static class ColumnAttributeTypeMapper
{
    // One registration per type. Lazy<T> (ExecutionAndPublication) runs the factory once and makes
    // every caller — including one that arrives while another thread is still registering the same
    // type — wait on Value until SqlMapper.SetTypeMap has run. GetOrAdd may construct a Lazy that is
    // never stored; that one is never started. Publishing the type before the map was in place let a
    // concurrent caller build a Dapper row parser on the default type map, which ignores [Column].
    private static readonly ConcurrentDictionary<Type, Lazy<bool>> Registrations = new();



    /// <summary>
    /// Registers a custom type map for <typeparamref name="T"/> that checks
    /// <see cref="ColumnAttribute"/> first, then falls back to Dapper's default
    /// name-based matching. Thread-safe — each type is registered at most once, and
    /// every caller, including one that arrives while another is registering the same
    /// type, returns only after the type map is in place.
    /// </summary>
    internal static void Register<[DynamicallyAccessedMembers(RecordMembers.Mapped)] T>()
    {
        _ = Registrations
            .GetOrAdd(typeof(T), _ => new Lazy<bool>(RegisterCore<T>))
            .Value;
    }



    private static bool RegisterCore<[DynamicallyAccessedMembers(RecordMembers.Mapped)] T>()
    {
        var type = typeof(T);
        var lookup = BuildLookup(type);
        if (lookup == null)
        {
            // No [Column] attributes anywhere → let Dapper's built-in name matcher handle it.
            return true;
        }

        var columnMap = new CustomPropertyTypeMap(type, (t, columnName) => ResolveColumn(t, columnName, lookup)!);
        SqlMapper.SetTypeMap(type, columnMap);
        return true;
    }



    private static Dictionary<string, PropertyInfo>? BuildLookup([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
    {
        var lookup = BuildColumnLookup(type, out var hasColumnAttributes);
        return hasColumnAttributes ? lookup : null;
    }



    /// <summary>
    /// Builds the case-insensitive column-to-property lookup for <paramref name="type"/>:
    /// every public instance property by name, overwritten by any <see cref="ColumnAttribute"/>
    /// name. Shared by the Dapper type map and the reflection row mapper so both resolve
    /// a column to the same property.
    /// </summary>
    internal static Dictionary<string, PropertyInfo> BuildColumnLookup
    (
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        out bool hasColumnAttributes
    )
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var lookup = new Dictionary<string, PropertyInfo>(props.Length * 2, StringComparer.OrdinalIgnoreCase);
        hasColumnAttributes = false;

        // First pass: property-name fallback entries.
        foreach (var prop in props)
        {
            lookup[prop.Name] = prop;
        }

        // Second pass: [Column] entries overwrite so explicit attribute wins on collisions.
        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<ColumnAttribute>();
            if (attr?.Name == null)
            {
                continue;
            }

            hasColumnAttributes = true;
            lookup[attr.Name] = prop;
        }

        return lookup;
    }



    internal static PropertyInfo? ResolveColumn(Type type, string columnName, Dictionary<string, PropertyInfo> lookup)
    {
        if (lookup.TryGetValue(columnName, out var prop))
        {
            return prop;
        }

        if (DbClientOptions.StrictColumnMapping)
        {
            // Strict mode: surface a self-describing error instead of letting
            // Dapper silently drop the column (its default behavior when the
            // type-map delegate returns null).
            throw new InvalidOperationException
            (
                $"Result-set column '{columnName}' does not map to any property on '{type.FullName}'. " +
                "Either add a property of that name, decorate an existing property with " +
                $"[Column(\"{columnName}\")], or alias the column in your SELECT statement. " +
                "Set DbClientOptions.StrictColumnMapping = false to silently drop unmapped columns."
            );
        }

        // Non-strict (default): return null so Dapper drops the unmapped column,
        // matching its out-of-the-box behavior.
        return null;
    }
}
