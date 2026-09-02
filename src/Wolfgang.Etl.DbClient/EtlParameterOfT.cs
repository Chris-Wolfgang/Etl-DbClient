using System;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// A command parameter with a typed value.
/// </summary>
/// <typeparam name="T">The value type. Output values are converted to this type.</typeparam>
/// <example>
/// <code>
/// // An input parameter carries its value.
/// var id = new EtlParameter&lt;int&gt; { Value = 42 };
///
/// // An output parameter declares its type and direction instead; the value
/// // arrives after the command runs.
/// var total = new EtlParameter&lt;int&gt;
/// {
///     DbType = System.Data.DbType.Int32,
///     Direction = System.Data.ParameterDirection.Output
/// };
///
/// // Supply them as dictionary values to a DbExtractor or DbLoader constructor,
/// // alongside plain values, which continue to bind as inputs.
/// var parameters = new System.Collections.Generic.Dictionary&lt;string, object&gt;
/// {
///     ["@CustomerId"] = id,
///     ["@TotalCount"] = total,
///     ["@Region"] = "EU"
/// };
///
/// // After extraction completes:
/// // var count = total.Value;
/// </code>
/// </example>
public sealed class EtlParameter<T> : EtlParameter
{
    /// <summary>
    /// Gets the value. Settable only in an object initializer; after execution an output
    /// parameter's value is written by parameter binding.
    /// </summary>
    public T? Value
    {
        get => StoredValue is null ? default : (T)StoredValue;
        init => StoredValue = value;
    }


    /// <inheritdoc/>
    internal override Type ValueType => typeof(T);
}
