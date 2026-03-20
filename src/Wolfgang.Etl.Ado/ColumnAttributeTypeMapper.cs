using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Dapper;

namespace Wolfgang.Etl.Ado;

/// <summary>
/// Configures Dapper to respect <see cref="ColumnAttribute"/> when mapping
/// result set columns to POCO properties. Without this, Dapper only matches
/// by property name.
/// </summary>
internal static class ColumnAttributeTypeMapper
{
    /// <summary>
    /// Registers a custom type map for <typeparamref name="T"/> that checks
    /// <see cref="ColumnAttribute"/> first, then falls back to Dapper's default
    /// name-based matching.
    /// </summary>
    internal static void Register<T>()
    {
        var type = typeof(T);

        // Only register if the type has any [Column] attributes
        var hasColumnAttributes = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.GetCustomAttribute<ColumnAttribute>() != null);

        if (!hasColumnAttributes)
        {
            return;
        }

        var columnMap = new CustomPropertyTypeMap
        (
            type,
            (t, columnName) =>
            {
                var props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // First try [Column("name")] match
                var prop = props.FirstOrDefault(p =>
                {
                    var attr = p.GetCustomAttribute<ColumnAttribute>();
                    return attr != null && string.Equals(attr.Name, columnName, StringComparison.OrdinalIgnoreCase);
                });

                // Fall back to property name match (case-insensitive, like Dapper default)
                prop ??= props.FirstOrDefault(p =>
                    string.Equals(p.Name, columnName, StringComparison.OrdinalIgnoreCase));

                return prop!;
            }
        );

        SqlMapper.SetTypeMap(type, columnMap);
    }
}
