// These BCL data interfaces predate nullable reference types: their property setters carry
// [AllowNull], which an implementation here cannot mirror because that attribute is shadowed by an
// internal polyfill in this project. CS8767/CS8769 are the resulting annotation-mismatch warnings
// and are suppressed for this file only — the members accept null exactly as the interfaces intend.
#pragma warning disable CS8767, CS8769

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// A minimal <see cref="IDbCommand"/> whose parameters accept any
/// <see cref="ParameterDirection"/>.
/// </summary>
/// <remarks>
/// Microsoft.Data.Sqlite rejects Output and InputOutput outright, so a real SQLite command cannot
/// exercise the output-parameter write-back at all. That logic is provider-independent, so a fake
/// is the honest double here rather than reaching for a provider that happens to permit it.
/// </remarks>
[ExcludeFromCodeCoverage]
internal sealed class FakeDbCommand : IDbCommand
{
    public IDataParameterCollection Parameters { get; } = new FakeParameterCollection();

    public IDbDataParameter CreateParameter() => new FakeParameter();

    public string CommandText { get; set; } = string.Empty;
    public int CommandTimeout { get; set; }
    public CommandType CommandType { get; set; }
    public IDbConnection? Connection { get; set; }
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() => throw new NotSupportedException();
    public int ExecuteNonQuery() => throw new NotSupportedException();
    public IDataReader ExecuteReader() => throw new NotSupportedException();
    public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
    public object? ExecuteScalar() => throw new NotSupportedException();
    public void Prepare() { }
    public void Dispose() { }
}



[ExcludeFromCodeCoverage]
internal sealed class FakeParameter : IDbDataParameter
{
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public int Size { get; set; }
    public DbType DbType { get; set; }
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public bool IsNullable => true;
    public string ParameterName { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public DataRowVersion SourceVersion { get; set; }
    public object? Value { get; set; }
}



[ExcludeFromCodeCoverage]
internal sealed class FakeParameterCollection : IDataParameterCollection
{
    private readonly List<object> _items = new();

    public object this[string parameterName]
    {
        get => _items[IndexOf(parameterName)];
        set => _items[IndexOf(parameterName)] = value;
    }

    public object? this[int index]
    {
        get => _items[index];
        set => _items[index] = value!;
    }

    public bool Contains(string parameterName) => IndexOf(parameterName) >= 0;

    public int IndexOf(string parameterName) =>
        _items.FindIndex(i => string.Equals(((IDbDataParameter)i).ParameterName, parameterName, StringComparison.Ordinal));

    public void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));

    public int Add(object? value) { _items.Add(value!); return _items.Count - 1; }
    public void Clear() => _items.Clear();
    public bool Contains(object? value) => _items.Contains(value!);
    public int IndexOf(object? value) => _items.IndexOf(value!);
    public void Insert(int index, object? value) => _items.Insert(index, value!);
    public void Remove(object? value) => _items.Remove(value!);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    public bool IsFixedSize => false;
    public bool IsReadOnly => false;
    public int Count => _items.Count;
    public bool IsSynchronized => false;
    public object SyncRoot => _items;
    public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    public IEnumerator GetEnumerator() => _items.GetEnumerator();
}
