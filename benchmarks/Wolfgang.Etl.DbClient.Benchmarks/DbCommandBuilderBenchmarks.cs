using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using Wolfgang.Etl.DbClient;

namespace Wolfgang.Etl.DbClient.Benchmarks;

/// <summary>
/// Microbenchmarks for the SQL command builder. Establishes the reflection-cost
/// baseline that the upcoming cache (review #7) will compare against. No I/O —
/// pure reflection + string concat.
/// </summary>
[MemoryDiagnoser]
public class DbCommandBuilderBenchmarks
{
    [Table("benchmark_people")]
    public class BenchmarkPerson
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("age")]
        public int Age { get; set; }

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }



    [Benchmark]
    public string BuildSelect() => DbCommandBuilder.BuildSelect<BenchmarkPerson>();



    [Benchmark]
    public string BuildInsert() => DbCommandBuilder.BuildInsert<BenchmarkPerson>();



    [Benchmark]
    public string BuildUpdate() => DbCommandBuilder.BuildUpdate<BenchmarkPerson>();
}
