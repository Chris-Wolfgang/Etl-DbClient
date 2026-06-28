// ReSharper disable UnusedAutoPropertyAccessor.Global -- consumed by Dapper / PublicAPI consumers via reflection (not visible to static analysis)

using System;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Marks a record / class as mapping to a database table. Triggers the
/// Wolfgang.Etl.DbClient source generator to emit a partial class with
/// reflection-free <c>Insert</c> SQL + parameter-binding helpers.
/// </summary>
/// <remarks>
/// <para>
/// The target type MUST be declared <c>partial</c> so the generator can add
/// members to it. Every public property maps to a column of the same name
/// unless overridden with <see cref="DbColumnAttribute"/>.
/// </para>
/// <para>
/// First v0.5.0 cut: <see cref="System.Type"/>-level analysis only — the
/// generator emits a <c>public const string Insert</c> and a static
/// <c>void Bind(Dapper.DynamicParameters parameters, T record)</c> helper.
/// Future versions will expand to <c>Update</c>, <c>Delete</c>, and
/// <c>Select</c> with proper integration into <c>DbExtractor</c> /
/// <c>DbLoader</c>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class DbTableAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="DbTableAttribute"/>.
    /// </summary>
    /// <param name="name">
    /// Table name as it appears in the database. Used as-is, no quoting —
    /// the caller is responsible for any provider-specific identifier
    /// escaping (square brackets, double quotes, backticks).
    /// </param>
    public DbTableAttribute(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>The table name supplied to the constructor.</summary>
    public string Name { get; }
}