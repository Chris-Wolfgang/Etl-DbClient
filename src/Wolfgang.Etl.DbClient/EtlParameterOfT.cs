using System;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// A command parameter with a typed value.
/// </summary>
/// <typeparam name="T">The value type. Output values are converted to this type.</typeparam>
/// <example>
/// <code>
/// var id    = new EtlParameter&lt;int&gt; { Value = 42 };
/// var total = new EtlParameter&lt;int&gt;
/// {
///     DbType = DbType.Int32,
///     Direction = ParameterDirection.Output
/// };
///
/// var extractor = new DbExtractor&lt;Order&gt;
/// (
///     connection,
///     "usp_GetOrdersForCustomer",
///     new Dictionary&lt;string, object&gt; { ["@CustomerId"] = id, ["@TotalCount"] = total },
///     new DbExtractorOptions { CommandType = CommandType.StoredProcedure }
/// );
///
/// await foreach (var order in extractor.ExtractAsync()) { }
/// var count = total.Value;
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
