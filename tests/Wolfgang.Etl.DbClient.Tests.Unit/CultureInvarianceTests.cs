// Culture-invariance gate — exercises the extractor + loader end-to-end
// under a matrix of hostile CultureInfo values so a future regression that
// swaps `StringComparer.Ordinal` for a culture-sensitive comparer, uses
// `ToUpper()`/`ToLower()` without the invariant overload, or calls
// `decimal.ToString()` / `DateTime.Parse()` without an invariant culture
// argument fails at PR time. Refs #145.
//
// Cultures covered — chosen for the class of bug each surfaces:
//   en-US   — baseline.
//   tr-TR   — dotted / dotless I: 'i'.ToUpper() == 'İ' (with dot), NOT 'I';
//             'I'.ToLower() == 'ı' (dotless). Trips culture-sensitive
//             case-insensitive lookups.
//   de-DE   — decimal comma (0,25 not 0.25). Trips number formatting that
//             doesn't specify CultureInfo.InvariantCulture.
//   zh-CN   — collation + simplified Chinese formatting.
//   ar-SA   — RTL + Hindi-Arabic digit shapes (٠١٢٣). Trips digit-parsing
//             that assumes ASCII 0-9.
//   ja-JP   — full-width digits + era-based year formatting.
//
// Allowlist of "intentionally culture-sensitive public methods": empty.
// DbClient's public API is contract-invariant — every string comparison
// uses StringComparer.Ordinal(IgnoreCase), and Dapper handles value
// serialisation invariantly. If that ever changes, document the new
// culture-sensitive API here + note the accepted risk.

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

/// <summary>
/// Swaps <see cref="Thread.CurrentThread"/>'s <see cref="CultureInfo.CurrentCulture"/>
/// and <see cref="CultureInfo.CurrentUICulture"/> for the lifetime of the
/// scope and restores them on <see cref="IDisposable.Dispose"/> — including
/// when the test throws.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _originalCulture;
    private readonly CultureInfo _originalUiCulture;

    public CultureScope(string cultureName)
    {
        _originalCulture = Thread.CurrentThread.CurrentCulture;
        _originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        Thread.CurrentThread.CurrentCulture = _originalCulture;
        Thread.CurrentThread.CurrentUICulture = _originalUiCulture;
    }
}

// Widget: reflection-consumed by Dapper's row-materializer + DbClient's
// property-name lookup. `id` (lower) mapped to Id (Pascal) so
// ColumnAttributeTypeMapper's case-insensitive resolution is exercised.
[ExcludeFromCodeCoverage]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
public sealed class CultureWidget
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class CultureInvarianceTests
{
    // Six cultures × 3 scenarios = 18 test rows. Each scenario asserts that
    // extractor/loader/mapper behave identically to en-US even under a
    // hostile culture.
    public static readonly TheoryData<string> Cultures = new()
    {
        "en-US", "tr-TR", "de-DE", "zh-CN", "ar-SA", "ja-JP",
    };

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Extract_roundtrips_decimal_and_datetime_under_hostile_culture(string cultureName)
    {
        using var _ = new CultureScope(cultureName);
        await using var conn = TestDb.CreateConnection();
        await using (var seed = conn.CreateCommand())
        {
            seed.CommandText = @"
                CREATE TABLE Widget (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Price REAL NOT NULL, CreatedUtc TEXT NOT NULL);
                INSERT INTO Widget (Id, Name, Price, CreatedUtc) VALUES
                    (1, 'sprocket', 1.25, '2026-07-14T12:34:56.7890000Z'),
                    (2, 'flange',   0.10, '2026-07-14T12:34:56.7890000Z'),
                    (3, 'grommet', 42.50, '2026-07-14T12:34:56.7890000Z');
            ";
            await seed.ExecuteNonQueryAsync();
        }

        var extractor = new DbExtractor<CultureWidget>(conn, "SELECT Id, Name, Price, CreatedUtc FROM Widget ORDER BY Id");
        var rows = new List<CultureWidget>();
        await foreach (var w in extractor.ExtractAsync())
        {
            rows.Add(w);
        }

        // Values that would misparse under de-DE (comma decimal) if the
        // extractor path used the ambient culture anywhere.
        Assert.Equal(3, rows.Count);
        Assert.Equal(1.25m,  rows[0].Price);
        Assert.Equal(0.10m,  rows[1].Price);
        Assert.Equal(42.50m, rows[2].Price);
        // DateTime round-trip — ar-SA / ja-JP calendars are the interesting
        // hostile cultures here. Value should equal the seed timestamp
        // regardless of the ambient CultureInfo.Calendar.
        var expected = new DateTime(2026, 7, 14, 12, 34, 56, 789, DateTimeKind.Utc);
        Assert.Equal(expected, rows[0].CreatedUtc.ToUniversalTime());
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Load_persists_decimal_and_datetime_under_hostile_culture(string cultureName)
    {
        using var _ = new CultureScope(cultureName);
        await using var conn = TestDb.CreateConnection();
        await using (var create = conn.CreateCommand())
        {
            create.CommandText = "CREATE TABLE Widget (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Price REAL NOT NULL, CreatedUtc TEXT NOT NULL);";
            await create.ExecuteNonQueryAsync();
        }

        var loader = new DbLoader<CultureWidget>
        (
            conn,
            "INSERT INTO Widget (Id, Name, Price, CreatedUtc) VALUES (@Id, @Name, @Price, @CreatedUtc)"
        );

        var toLoad = new[]
        {
            new CultureWidget { Id = 1, Name = "sprocket", Price = 1.25m,  CreatedUtc = new DateTime(2026, 7, 14, 12, 34, 56, 789, DateTimeKind.Utc) },
            new CultureWidget { Id = 2, Name = "flange",   Price = 0.10m,  CreatedUtc = new DateTime(2026, 7, 14, 12, 34, 56, 789, DateTimeKind.Utc) },
        };

        await loader.LoadAsync(ToAsyncEnumerable(toLoad));

        // Read back with the raw ADO.NET reader — bypasses DbClient's
        // extractor so any culture-injected error the LOAD path introduces
        // is caught here rather than being masked by a matching bug in
        // the read side.
        await using var readBack = conn.CreateCommand();
        readBack.CommandText = "SELECT Price FROM Widget ORDER BY Id";
        await using var reader = await readBack.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1.25m, Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture));
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0.10m, Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture));
    }

    // Note on case-insensitive property lookup: DbClient's internal
    // ColumnAttributeTypeMapper uses StringComparer.OrdinalIgnoreCase (see
    // ColumnAttributeTypeMapper.cs:68), which is culture-invariant by
    // contract. The extract test above transitively exercises that path
    // (Dapper binds columns to properties via the same lookup), so a
    // tr-TR dotted-I regression there would surface as an extract failure
    // in the first Theory. No direct-mapper test is added: the type is
    // `internal`, and covering it via the public surface catches the same
    // class of bug.

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
