using System;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Compares parameter names conservatively enough to be safe on every supported provider.
/// </summary>
/// <remarks>
/// This deliberately does NOT reproduce any one provider's rules — no single provider behaves
/// exactly like this. It is the union: a name pair is treated as colliding if <i>any</i>
/// supported provider would treat it as one parameter.
/// <para>
/// Supplying both <c>@PageLimit</c> and <c>@pagelimit</c> to one command was measured against
/// live engines. SQL Server, PostgreSQL and MySQL all resolve the name case-insensitively and
/// quietly use whichever arrived last, returning wrong rows with no error at all. Only SQLite
/// treats the two as distinct. An ordinal comparison therefore lets a caller-supplied name
/// shadow a generated one on three of the four engines the suite covers.
/// </para>
/// <para>
/// The trade-off is accepted knowingly: on SQLite this rejects a pair the engine would have
/// accepted. Refusing an ambiguous name there is a loud, immediate error the caller can fix,
/// whereas the alternative is silently wrong results on the other three engines.
/// </para>
/// <para>
/// The leading <c>@</c> is optional on both sides because Dapper stores names without it,
/// so <c>PageLimit</c> and <c>@PageLimit</c> reach the provider as the same parameter.
/// </para>
/// </remarks>
internal static class ParameterName
{
    /// <summary>
    /// Determines whether two parameter names could reach any supported provider as one parameter.
    /// </summary>
    /// <param name="left">The first name, with or without a leading <c>@</c>.</param>
    /// <param name="right">The second name, with or without a leading <c>@</c>.</param>
    /// <returns>
    /// <see langword="true"/> when at least one supported provider would treat them as a single
    /// parameter. Case-insensitive, so this is stricter than SQLite alone — see the type remarks.
    /// </returns>
    public static bool Matches(string? left, string? right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }



    private static string Normalize(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        return name![0] == '@' ? name.Substring(1) : name;
    }
}
