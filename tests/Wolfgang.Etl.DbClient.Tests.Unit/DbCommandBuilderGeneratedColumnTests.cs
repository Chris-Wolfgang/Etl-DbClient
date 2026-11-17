using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbCommandBuilderGeneratedColumnTests
{
    [ExcludeFromCodeCoverage]
    [Table("Invoices")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    private sealed class InvoiceRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Customer { get; set; } = string.Empty;

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public decimal Total { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Sequence { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Note { get; set; } = string.Empty;
    }



    [ExcludeFromCodeCoverage]
    [Table("OnlyComputed")]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    private sealed class OnlyComputedRecord
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int Value { get; set; }
    }



    [Fact]
    public void BuildInsert_excludes_computed_and_non_key_identity_columns()
    {
        Assert.Equal
        (
            "INSERT INTO Invoices (Customer, Note) VALUES (@Customer, @Note)",
            DbCommandBuilder.BuildInsert<InvoiceRecord>()
        );
    }



    [Fact]
    public void BuildUpdate_excludes_computed_and_non_key_identity_columns_from_SET()
    {
        Assert.Equal
        (
            "UPDATE Invoices SET Customer = @Customer, Note = @Note WHERE Id = @Id",
            DbCommandBuilder.BuildUpdate<InvoiceRecord>()
        );
    }



    [Fact]
    public void BuildSelect_keeps_database_generated_columns()
    {
        var sql = DbCommandBuilder.BuildSelect<InvoiceRecord>();

        Assert.Contains("Total", sql, StringComparison.Ordinal);
        Assert.Contains("Sequence", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildInsert_when_the_only_mapped_columns_are_computed_throws_the_no_mapped_columns_error()
    {
        var ex = Assert.Throws<InvalidOperationException>(DbCommandBuilder.BuildInsert<OnlyComputedRecord>);

        Assert.Contains("no mapped columns for INSERT", ex.Message, StringComparison.Ordinal);
    }
}
