using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Dapper;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Binds a caller-supplied parameter dictionary onto a command, dispatching on each value's type.
/// </summary>
/// <remarks>
/// This is the only place in the package aware of the underlying data-access library's parameter
/// hooks. Swapping that library means rewriting this class and nothing else — no public type
/// changes, because <see cref="EtlParameter"/> and the dictionary are BCL-only.
/// <para>
/// Implements the post-execution callback as well as parameter creation: output values are written
/// back once the command completes, which parameter creation alone cannot do.
/// </para>
/// </remarks>
internal sealed class EtlParameterSet : SqlMapper.IDynamicParameters, SqlMapper.IParameterCallbacks
{
    private readonly IDictionary<string, object> _source;
    private readonly List<KeyValuePair<EtlParameter, IDbDataParameter>> _writeBack = new();

    // Parameters the extractor adds itself (server paging). Kept separate so the caller's own
    // dictionary is never mutated - it is their object, and they may reuse it.
    private readonly Dictionary<string, object> _added = new(StringComparer.Ordinal);


    /// <summary>Initializes a new instance binding <paramref name="source"/>.</summary>
    /// <param name="source">The caller's parameter dictionary.</param>
    internal EtlParameterSet(IDictionary<string, object> source) => _source = source;


    /// <summary>
    /// True when <paramref name="source"/> contains a value needing this binder rather than the
    /// library's own dictionary handling.
    /// </summary>
    /// <param name="source">The dictionary to inspect.</param>
    /// <returns><c>true</c> when any value is an <see cref="EtlParameter"/> or <see cref="DbParameter"/>.</returns>
    internal static bool IsNeededFor(IDictionary<string, object> source)
    {
        foreach (var value in source.Values)
        {
            if (value is EtlParameter or DbParameter)
            {
                return true;
            }
        }

        return false;
    }


    /// <inheritdoc/>
    public void AddParameters(IDbCommand command, SqlMapper.Identity identity)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        foreach (var entry in Entries())
        {
            switch (entry.Value)
            {
                // The caller's own provider parameter: attach the instance itself, so the provider
                // writes any output value straight back into the object they still hold.
                case DbParameter supplied:
                    command.Parameters.Add(supplied);
                    break;

                case EtlParameter described:
                    command.Parameters.Add(Materialize(command, entry.Key, described));
                    break;

                default:
                    var plain = command.CreateParameter();
                    plain.ParameterName = entry.Key;
                    plain.Value = entry.Value ?? (object)DBNull.Value;
                    command.Parameters.Add(plain);
                    break;
            }
        }
    }


    /// <inheritdoc/>
    /// <remarks>
    /// Runs after the command completes. Only <see cref="EtlParameter"/> values need this — a
    /// caller-supplied <see cref="DbParameter"/> is the command's own parameter, so the provider
    /// has already populated it.
    /// </remarks>
    public void OnCompleted()
    {
        foreach (var pair in _writeBack)
        {
            pair.Key.SetValue(pair.Value.Value);
        }
    }


    /// <summary>Adds a parameter the extractor supplies itself, without touching the caller's dictionary.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    internal void Add(string name, object value) => _added[name] = value;


    private IEnumerable<KeyValuePair<string, object>> Entries()
    {
        foreach (var entry in _source)
        {
            yield return entry;
        }

        foreach (var entry in _added)
        {
            yield return entry;
        }
    }


    private IDbDataParameter Materialize(IDbCommand command, string name, EtlParameter described)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;

        if (described.DbType.HasValue)
        {
            parameter.DbType = described.DbType.Value;
        }

        // Assigning Direction can throw for providers that reject it (Microsoft.Data.Sqlite rejects
        // Output outright). That is deliberate: the provider's own message names the problem.
        parameter.Direction = described.Direction;

        if (described.Size.HasValue)
        {
            parameter.Size = described.Size.Value;
        }

        parameter.Value = described.BoxedValue ?? DBNull.Value;

        if (described.Direction != ParameterDirection.Input)
        {
            _writeBack.Add(new KeyValuePair<EtlParameter, IDbDataParameter>(described, parameter));
        }

        return parameter;
    }
}
