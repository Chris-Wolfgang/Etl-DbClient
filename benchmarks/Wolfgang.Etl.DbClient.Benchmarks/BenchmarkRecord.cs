using System.ComponentModel.DataAnnotations.Schema;

namespace Wolfgang.Etl.DbClient.Benchmarks;

/// <summary>
/// Benchmark row mapped to the cross-dialect <c>contract_items</c> schema used by
/// both the integration suite and the benchmark suite. Lower-case identifiers
/// avoid Postgres' unquoted-folding behaviour.
/// </summary>
[Table("contract_items")]
public class BenchmarkRecord
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("value")]
    public int Value { get; set; }
}
