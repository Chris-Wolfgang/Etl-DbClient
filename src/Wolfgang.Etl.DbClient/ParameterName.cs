using System;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Compares parameter names the way a database provider does.
/// </summary>
/// <remarks>
/// Providers do not agree with .NET's default string comparison here, and the disagreement
/// is silent. Supplying both <c>@PageLimit</c> and <c>@pagelimit</c> to one command was
/// measured against live engines: SQL Server, PostgreSQL and MySQL all resolve the name
/// case-insensitively and quietly use whichever arrived last, returning wrong rows with no
/// error; only SQLite treats the two as distinct. An ordinal comparison therefore lets a
/// caller-supplied name shadow a generated one on three of the four engines the suite covers.
/// <para>
/// The leading <c>@</c> is optional on both sides because Dapper stores names without it,
/// so <c>PageLimit</c> and <c>@PageLimit</c> reach the provider as the same parameter.
/// </para>
/// </remarks>
internal static class ParameterName
{
    /// <summary>
    /// Determines whether two parameter names would reach the provider as the same parameter.
    /// </summary>
    /// <param name="left">The first name, with or without a leading <c>@</c>.</param>
    /// <param name="right">The second name, with or without a leading <c>@</c>.</param>
    /// <returns><see langword="true"/> when the provider would treat them as one parameter.</returns>
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
