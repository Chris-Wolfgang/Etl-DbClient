using System;
using System.Collections.Generic;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Default value of a constructor parameter's value type, for a <see cref="DBNull"/>
/// column. Kept to the value types a column can carry, so no reflection over
/// arbitrary types is needed to box a default.
/// </summary>
/// <remarks>
/// Lives outside the generic <see cref="ReflectionRowMapper{TRecord}"/> so there is one table rather
/// than one per closed <c>TRecord</c> (InspectCode StaticMemberInGenericType).
/// </remarks>
internal static class DefaultValueTypes
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
