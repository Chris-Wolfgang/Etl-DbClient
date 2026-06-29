using System;
using JetBrains.Annotations;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Optional per-property override for a <see cref="DbTableAttribute"/>-decorated
/// record. Maps the annotated property to a column whose name differs from
/// the property name, or marks it as <see cref="Skip"/>ped from the
/// generated SQL.
/// </summary>
/// <remarks>
/// <c>[PublicAPI]</c> marks the attribute and its members as consumed by
/// downstream NuGet consumers + the DbTableGenerator source generator —
/// ReSharper has no visibility into those external/generated readers.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
[PublicAPI]
public sealed class DbColumnAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="DbColumnAttribute"/>.
    /// </summary>
    /// <param name="name">Column name as it appears in the database.</param>
    public DbColumnAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>The column name supplied to the constructor.</summary>
    public string Name { get; }

    /// <summary>
    /// When <see langword="true"/>, the property is excluded from the
    /// generated <c>Insert</c> SQL and parameter binder. Useful for computed
    /// columns or columns the application doesn't write.
    /// </summary>
    public bool Skip { get; set; }
}
