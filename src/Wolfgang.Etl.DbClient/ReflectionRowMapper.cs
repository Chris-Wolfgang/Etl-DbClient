using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Materializes <typeparamref name="TRecord"/> rows from a <see cref="DbDataReader"/> with
/// plain reflection (<see cref="ConstructorInfo.Invoke(object[])"/> and
/// <see cref="PropertyInfo.SetValue(object, object)"/>) instead of Dapper's
/// Reflection.Emit row parser.
/// </summary>
/// <remarks>
/// <para>
/// This is the row-mapping path used when the runtime cannot generate code
/// (<c>RuntimeFeature.IsDynamicCodeSupported</c> is <see langword="false"/>, i.e. a
/// Native AOT application), where Dapper's <c>GetRowParser</c> throws
/// <see cref="PlatformNotSupportedException"/>. On the JIT the extractor keeps using
/// Dapper's emitted parser, which is faster.
/// </para>
/// <para>
/// It follows Dapper's mapping rules for the shapes this library documents: columns map
/// to public instance properties by <see cref="System.ComponentModel.DataAnnotations.Schema.ColumnAttribute"/>
/// name, then by property name (case-insensitive), resolved through the same lookup as
/// the Dapper type map (<see cref="ColumnAttributeTypeMapper"/>), so
/// <see cref="DbClientOptions.StrictColumnMapping"/> applies identically; a
/// <see cref="DBNull"/> value leaves the property at its initializer value; a public
/// parameterless constructor is preferred, otherwise the public constructor whose
/// parameters all name a column is used; a scalar <typeparamref name="TRecord"/>
/// (primitive, <see cref="string"/>, <see cref="decimal"/>, enum, ...) reads the first
/// column. A value that cannot be converted to the member type is reported as a
/// <see cref="DataException"/>, the type Dapper uses, so
/// <see cref="DbExtractor{TRecord}"/> error handling sees the same exception either way.
/// </para>
/// <para>
/// Not supported (Dapper-only, JIT path): value tuples, public fields, non-public
/// properties, and Dapper custom type handlers.
/// </para>
/// </remarks>
internal static class ReflectionRowMapper<[DynamicallyAccessedMembers(RecordMembers.Mapped)] TRecord>
{
    /// <summary>
    /// Builds a row parser bound to the column layout of <paramref name="reader"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TRecord"/> has no constructor the columns can satisfy, or a column
    /// does not map to a property while <see cref="DbClientOptions.StrictColumnMapping"/> is on.
    /// </exception>
    internal static Func<DbDataReader, TRecord> CreateParser(DbDataReader reader)
    {
        var type = typeof(TRecord);
        if (IsScalar(type))
        {
            return r => (TRecord)ReadScalar(r);
        }

        var columnNames = new string[reader.FieldCount];
        for (var i = 0; i < columnNames.Length; i++)
        {
            columnNames[i] = reader.GetName(i);
        }

        var lookup = ColumnAttributeTypeMapper.BuildColumnLookup(type, out _);
        var parameterless = FindParameterlessConstructor(type);
        if (parameterless != null || type.IsValueType)
        {
            var setters = BuildSetters(type, columnNames, lookup);
            return r => Materialize(r, parameterless, setters);
        }

        var constructor = FindColumnConstructor(type, columnNames, out var ordinals);
        return r => Construct(r, constructor, ordinals);
    }



    private static TRecord Materialize(DbDataReader reader, ConstructorInfo? constructor, PropertyInfo?[] setters)
    {
        var instance = constructor != null
            ? constructor.Invoke(parameters: null)
            : Activator.CreateInstance(typeof(TRecord))!;

        for (var i = 0; i < setters.Length; i++)
        {
            var property = setters[i];
            if (property == null)
            {
                continue;
            }

            var value = reader.GetValue(i);
            if (value is DBNull)
            {
                continue;
            }

            property.SetValue(instance, ConvertColumn(reader, i, value, property.PropertyType));
        }

        return (TRecord)instance;
    }



    private static TRecord Construct(DbDataReader reader, ConstructorInfo constructor, int[] ordinals)
    {
        var parameters = constructor.GetParameters();
        var args = new object?[parameters.Length];
        for (var p = 0; p < parameters.Length; p++)
        {
            var ordinal = ordinals[p];
            var value = reader.GetValue(ordinal);
            var parameterType = parameters[p].ParameterType;
            args[p] = value is DBNull
                ? DefaultOf(parameterType)
                : ConvertColumn(reader, ordinal, value, parameterType);
        }

        return (TRecord)constructor.Invoke(args);
    }



    private static object ReadScalar(DbDataReader reader)
    {
        var value = reader.GetValue(0);
        return (value is DBNull
            ? DefaultOf(typeof(TRecord))
            : ConvertColumn(reader, 0, value, typeof(TRecord)))!;
    }



    private static PropertyInfo?[] BuildSetters
    (
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        string[] columnNames,
        Dictionary<string, PropertyInfo> lookup
    )
    {
        var setters = new PropertyInfo?[columnNames.Length];
        for (var i = 0; i < columnNames.Length; i++)
        {
            var property = ColumnAttributeTypeMapper.ResolveColumn(type, columnNames[i], lookup);
            setters[i] = property is { CanWrite: true } ? property : null;
        }

        return setters;
    }



    private static ConstructorInfo? FindParameterlessConstructor
    (
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    ) => type.GetConstructor(Type.EmptyTypes);



    private static ConstructorInfo FindColumnConstructor
    (
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type,
        string[] columnNames,
        out int[] ordinals
    )
    {
        foreach (var constructor in type.GetConstructors().OrderByDescending(c => c.GetParameters().Length))
        {
            var parameters = constructor.GetParameters();
            ordinals = new int[parameters.Length];
            var matched = true;
            for (var p = 0; p < parameters.Length && matched; p++)
            {
                ordinals[p] = Array.FindIndex(columnNames, c => string.Equals(c, parameters[p].Name, StringComparison.OrdinalIgnoreCase));
                matched = ordinals[p] >= 0;
            }

            if (matched)
            {
                return constructor;
            }
        }

        throw new InvalidOperationException
        (
            $"'{type.FullName}' has no public parameterless constructor and no public constructor whose " +
            $"parameters all name a column of the result set ({string.Join(", ", columnNames)})."
        );
    }



    private static object? ConvertColumn(DbDataReader reader, int ordinal, object value, Type targetType)
    {
        try
        {
            return ConvertValue(value, targetType);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
        {
            throw new DataException
            (
                $"Error parsing column {ordinal.ToString(CultureInfo.InvariantCulture)} " +
                $"({reader.GetName(ordinal)}={Convert.ToString(value, CultureInfo.InvariantCulture)} - {value.GetType().FullName}) " +
                $"as {targetType.FullName}",
                ex
            );
        }
    }



    internal static object? ConvertValue(object value, Type targetType)
    {
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        if (type.IsEnum)
        {
            return value is string name
                ? Enum.Parse(type, name, ignoreCase: true)
                : Enum.ToObject(type, Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture));
        }

        if (type == typeof(Guid))
        {
            return value switch
            {
                string text => Guid.Parse(text),
                byte[] bytes => new Guid(bytes),
                _ => throw new InvalidCastException($"Cannot convert {value.GetType().FullName} to {typeof(Guid).FullName}."),
            };
        }

        if (type == typeof(char) && value is string { Length: 1 } single)
        {
            return single[0];
        }

        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }



    private static object? DefaultOf(Type type) =>
        type.IsValueType && Nullable.GetUnderlyingType(type) == null
            ? DefaultValueTypes.GetOrCreate(type)
            : null;



    private static bool IsScalar(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid)
            || underlying == typeof(byte[]);
    }



    /// <summary>
    /// Default value of a constructor parameter's value type, for a <see cref="DBNull"/>
    /// column. Kept to the value types a column can carry, so no reflection over
    /// arbitrary types is needed to box a default.
    /// </summary>
    private static class DefaultValueTypes
    {
        private static readonly Dictionary<Type, object> Defaults = new()
        {
            [typeof(bool)] = false,
            [typeof(byte)] = (byte)0,
            [typeof(sbyte)] = (sbyte)0,
            [typeof(short)] = (short)0,
            [typeof(ushort)] = (ushort)0,
            [typeof(int)] = 0,
            [typeof(uint)] = 0U,
            [typeof(long)] = 0L,
            [typeof(ulong)] = 0UL,
            [typeof(float)] = 0F,
            [typeof(double)] = 0D,
            [typeof(decimal)] = 0M,
            [typeof(char)] = '\0',
            [typeof(DateTime)] = default(DateTime),
            [typeof(DateTimeOffset)] = default(DateTimeOffset),
            [typeof(TimeSpan)] = default(TimeSpan),
            [typeof(Guid)] = default(Guid),
        };



        internal static object GetOrCreate(Type type)
        {
            if (Defaults.TryGetValue(type, out var value))
            {
                return value;
            }

            if (type.IsEnum)
            {
                return Enum.ToObject(type, 0);
            }

            throw new NotSupportedException
            (
                $"A NULL column cannot be passed to a constructor parameter of type '{type.FullName}'."
            );
        }
    }
}
