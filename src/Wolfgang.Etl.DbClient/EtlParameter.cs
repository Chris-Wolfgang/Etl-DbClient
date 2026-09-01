using System;
using System.Data;
using System.Globalization;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Base class for a command parameter described independently of any ORM or provider.
/// </summary>
/// <remarks>
/// Supply instances as values in the <c>IDictionary&lt;string, object&gt;</c> passed to a
/// <see cref="DbExtractor{TRecord}"/> or <see cref="DbLoader{TRecord}"/> constructor. A dictionary
/// entry whose value is not an <see cref="EtlParameter"/> (and not a
/// <see cref="System.Data.Common.DbParameter"/>) is treated as a plain input value, exactly as before.
/// <para>
/// This type exists so that direction, <see cref="System.Data.DbType"/> and size can be expressed
/// without exposing a third-party parameter type on this package's public surface — which is what
/// keeps the underlying data-access library replaceable.
/// </para>
/// <para>
/// Use the generic <see cref="EtlParameter{T}"/>; this base carries only what parameter binding
/// needs to read without knowing the value's type.
/// </para>
/// </remarks>
public abstract class EtlParameter
{
    /// <summary>The stored value. Written by parameter binding after execution for output directions.</summary>
    private protected object? StoredValue;


    /// <summary>
    /// Gets the provider type. When <c>null</c> the provider infers it from the value — which is
    /// not possible for an output parameter, so outputs should set it explicitly.
    /// </summary>
    public DbType? DbType { get; init; }


    /// <summary>
    /// Gets the parameter direction. Defaults to <see cref="ParameterDirection.Input"/>.
    /// </summary>
    /// <remarks>
    /// Not every provider supports every direction, and the check happens when this parameter is
    /// mapped onto a provider parameter — that is, when extraction or loading runs, not when this
    /// object is constructed. Microsoft.Data.Sqlite, for example, rejects
    /// <see cref="ParameterDirection.Output"/> outright.
    /// </remarks>
    public ParameterDirection Direction { get; init; } = ParameterDirection.Input;


    /// <summary>Gets the parameter size, or <c>null</c> to let the provider decide.</summary>
    public int? Size { get; init; }


    /// <summary>The value as <see cref="object"/>, for binding code that does not know the type.</summary>
    internal object? BoxedValue => StoredValue;


    /// <summary>The declared value type, used to reconcile what the provider returns.</summary>
    internal abstract Type ValueType { get; }


    /// <summary>
    /// Stores a value produced by the provider, converting it to the declared type when they differ.
    /// </summary>
    /// <remarks>
    /// Providers do not always return the declared type — SQLite yields <see cref="long"/> for
    /// INTEGER, SQL Server yields <see cref="decimal"/> for NUMERIC. Converting here means the
    /// caller reads the type they asked for instead of an <see cref="InvalidCastException"/> from
    /// the getter.
    /// </remarks>
    /// <param name="value">The value the provider produced.</param>
    /// <exception cref="InvalidOperationException">The value cannot be converted to the declared type.</exception>
    internal void SetValue(object? value)
    {
        if (value is null || value is DBNull)
        {
            StoredValue = null;
            return;
        }

        var target = Nullable.GetUnderlyingType(ValueType) ?? ValueType;

        if (target.IsInstanceOfType(value))
        {
            StoredValue = value;
            return;
        }

        try
        {
            // Convert.ChangeType cannot target an enum - it throws InvalidCastException for
            // integral and string sources alike - so enums are converted explicitly. Providers
            // return the underlying integral type for a column storing an enum.
            if (!target.IsEnum)
            {
                StoredValue = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            }
            else if (value is string text)
            {
                StoredValue = Enum.Parse(target, text, ignoreCase: true);
            }
            else
            {
                StoredValue = Enum.ToObject(target, value);
            }
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidOperationException
            (
                $"The provider returned '{value.GetType().FullName}' for a parameter declared as " +
                $"'{ValueType.FullName}', and the value could not be converted. Declare the " +
                "parameter with the type the provider returns.",
                ex
            );
        }
    }
}
