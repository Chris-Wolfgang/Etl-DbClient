using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using Dapper;

namespace Wolfgang.Etl.DbClient.Benchmarks;

/// <summary>
/// Microbenchmarks for <see cref="ColumnAttributeTypeMapper"/>'s per-column
/// lookup path. Dapper invokes the registered <see cref="CustomPropertyTypeMap"/>
/// delegate once per result-set column per row, so a fast lookup matters under load.
/// </summary>
[MemoryDiagnoser]
public class ColumnAttributeTypeMapperBenchmarks
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

    private static readonly string[] ColumnNames =
    {
        "id", "first_name", "last_name", "age", "email", "created_at"
    };

    private SqlMapper.ITypeMap _typeMap = null!;

    [GlobalSetup]
    public void Setup()
    {
        ColumnAttributeTypeMapper.Register<BenchmarkPerson>();
        _typeMap = SqlMapper.GetTypeMap(typeof(BenchmarkPerson));
    }


    /// <summary>
    /// Simulates one row's worth of column lookups (6 columns).
    /// Multiply by row count to estimate full-query cost.
    /// </summary>
    [Benchmark]
    public int LookupAllColumnsOnce()
    {
        var hits = 0;
        foreach (var name in ColumnNames)
        {
            if (_typeMap.GetMember(name) != null)
            {
                hits++;
            }
        }
        return hits;
    }
}
