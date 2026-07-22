using System;
using JetBrains.Annotations;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Marks a property on a <see cref="DbTableAttribute"/>-decorated record as
/// part of the row's identifying key. The <c>DbTableGenerator</c> source
/// generator uses <c>[DbKey]</c> properties to build the WHERE clause of
/// the generated <c>Update</c> and <c>Delete</c> SQL constants.
/// </summary>
/// <remarks>
/// <para>
/// A type may carry more than one <c>[DbKey]</c> property to model a
/// composite key. Column ordering in the emitted WHERE clause matches
/// the property declaration order.
/// </para>
/// <para>
/// <c>[DbKey]</c> is orthogonal to <see cref="DbColumnAttribute"/>:
/// a key property may also carry a <c>[DbColumn]</c> override for the
/// SQL column name, and the aliasing rule matches the runtime
/// <c>DbCommandBuilder</c> emitter.
/// </para>
/// <para>
/// <c>[PublicAPI]</c> marks the attribute as consumed by downstream NuGet
/// consumers + the DbTableGenerator source generator — ReSharper has no
/// visibility into those external / generated readers.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
[PublicAPI]
public sealed class DbKeyAttribute : Attribute
{
}
