using System;

namespace Wolfgang.Etl.DbClient;

/// <summary>
/// Converts a <see cref="TimeSpan"/> command timeout to the whole seconds ADO.NET and Dapper take.
/// Shared by the extractor and the loader so the two cannot drift.
/// </summary>
internal static class CommandTimeoutConverter
{
    /// <summary>Converts <paramref name="timeout"/> to whole seconds.</summary>
    /// <param name="timeout">The configured timeout, or <c>null</c> for the provider default.</param>
    /// <returns>
    /// <c>null</c> for <c>null</c>; 0 for <see cref="TimeSpan.Zero"/> (the provider's "no timeout");
    /// otherwise the value rounded UP to whole seconds - truncating would turn a sub-second
    /// timeout into 0, an unbounded wait - and clamped to <see cref="int.MaxValue"/>.
    /// </returns>
    internal static int? ToSeconds(TimeSpan? timeout)
    {
        if (!timeout.HasValue)
        {
            return null;
        }

        var seconds = Math.Ceiling(timeout.Value.TotalSeconds);

        return seconds >= int.MaxValue
            ? int.MaxValue
            : (int)seconds;
    }
}
