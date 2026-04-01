using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class DbCommandBuilderTests
{
    // ------------------------------------------------------------------
    // Test types
    // ------------------------------------------------------------------

    [ExcludeFromCodeCoverage]
    [Table("Customers")]
    private class CustomerRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("customer_id")]
        public int Id { get; set; }

        [Column("full_name")]
        public string Name { get; set; } = string.Empty;

        [Column("email_address")]
        public string Email { get; set; } = string.Empty;

        [NotMapped]
        public string TempValue { get; set; } = string.Empty;
    }



    [ExcludeFromCodeCoverage]
    [Table("Orders")]
    private class CompositeKeyRecord
    {
        [Key]
        [Column("order_id")]
        public int OrderId { get; set; }

        [Key]
        [Column("line_number")]
        public int LineNumber { get; set; }

        [Column("quantity")]
        public int Quantity { get; set; }
    }



    [ExcludeFromCodeCoverage]
    private class NoTableRecord
    {
    }



    [ExcludeFromCodeCoverage]
    [Table("Items")]
    private class NoKeyRecord
    {
        [Column("item_name")]
        public string Name { get; set; } = string.Empty;

        [Column("price")]
        public decimal Price { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [Table("AllNotMapped")]
    private class AllNotMappedRecord
    {
        [NotMapped]
        public int Id { get; set; }

        [NotMapped]
        public string Name { get; set; } = string.Empty;
    }



    [ExcludeFromCodeCoverage]
    [Table("AllIdentity")]
    private class AllIdentityKeysRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [Table("AllKeysOnly")]
    private class AllKeysOnlyRecord
    {
        [Key]
        [Column("key_a")]
        public int KeyA { get; set; }

        [Key]
        [Column("key_b")]
        public int KeyB { get; set; }
    }



    [ExcludeFromCodeCoverage]
    [Table("NotMappedKey")]
    private class NotMappedKeyRecord
    {
        [Key]
        [NotMapped]
        public int Id { get; set; }

        [Column("value")]
        public string Value { get; set; } = string.Empty;
    }



    // ------------------------------------------------------------------
    // BuildSelect
    // ------------------------------------------------------------------

    [Fact]
    public void BuildSelect_generates_select_with_column_aliases()
    {
        var sql = DbCommandBuilder.BuildSelect<CustomerRecord>();

        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Customers", sql, StringComparison.Ordinal);
        Assert.Contains("customer_id", sql, StringComparison.Ordinal);
        Assert.Contains("full_name", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("TempValue", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildSelect_when_all_properties_are_NotMapped_returns_select_star()
    {
        var sql = DbCommandBuilder.BuildSelect<AllNotMappedRecord>();

        Assert.Contains("SELECT * FROM AllNotMapped", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildSelect_when_no_Table_attribute_throws_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>
        (
            DbCommandBuilder.BuildSelect<NoTableRecord>
        );
    }



    // ------------------------------------------------------------------
    // BuildInsert
    // ------------------------------------------------------------------

    [Fact]
    public void BuildInsert_excludes_identity_key_columns()
    {
        var sql = DbCommandBuilder.BuildInsert<CustomerRecord>();

        Assert.Contains("INSERT INTO Customers", sql, StringComparison.Ordinal);
        Assert.Contains("full_name", sql, StringComparison.Ordinal);
        Assert.Contains("@Name", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("customer_id", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("TempValue", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildInsert_includes_non_identity_key_columns()
    {
        var sql = DbCommandBuilder.BuildInsert<CompositeKeyRecord>();

        Assert.Contains("order_id", sql, StringComparison.Ordinal);
        Assert.Contains("line_number", sql, StringComparison.Ordinal);
        Assert.Contains("quantity", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildInsert_with_no_key_includes_all_columns()
    {
        var sql = DbCommandBuilder.BuildInsert<NoKeyRecord>();

        Assert.Contains("item_name", sql, StringComparison.Ordinal);
        Assert.Contains("price", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildInsert_when_all_keys_are_identity_falls_back_to_all_columns()
    {
        var sql = DbCommandBuilder.BuildInsert<AllIdentityKeysRecord>();

        // All columns are identity keys, so fallback includes them all
        Assert.Contains("INSERT INTO AllIdentity", sql, StringComparison.Ordinal);
        Assert.Contains("id", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildInsert_when_no_Table_attribute_throws_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>
        (
            DbCommandBuilder.BuildInsert<NoTableRecord>
        );
    }



    [Fact]
    public void BuildInsert_when_all_properties_are_NotMapped_throws_InvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>
        (
            DbCommandBuilder.BuildInsert<AllNotMappedRecord>
        );

        Assert.Contains
        (
            "no mapped columns",
            ex.Message
        );
    }



    // ------------------------------------------------------------------
    // BuildUpdate
    // ------------------------------------------------------------------

    [Fact]
    public void BuildUpdate_uses_key_columns_in_where_clause()
    {
        var sql = DbCommandBuilder.BuildUpdate<CustomerRecord>();

        Assert.Contains("UPDATE Customers SET", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE customer_id = @Id", sql, StringComparison.Ordinal);
        Assert.Contains("full_name = @Name", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("TempValue", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildUpdate_with_composite_key_includes_all_keys_in_where()
    {
        var sql = DbCommandBuilder.BuildUpdate<CompositeKeyRecord>();

        Assert.Contains("WHERE", sql, StringComparison.Ordinal);
        Assert.Contains("order_id = @OrderId", sql, StringComparison.Ordinal);
        Assert.Contains("line_number = @LineNumber", sql, StringComparison.Ordinal);
    }



    [Fact]
    public void BuildUpdate_when_no_key_throws_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>
        (
            DbCommandBuilder.BuildUpdate<NoKeyRecord>
        );
    }



    [Fact]
    public void BuildUpdate_when_no_Table_attribute_throws_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>
        (
            DbCommandBuilder.BuildUpdate<NoTableRecord>
        );
    }



    [Fact]
    public void BuildUpdate_when_all_columns_are_keys_throws_InvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>
        (
            DbCommandBuilder.BuildUpdate<AllKeysOnlyRecord>
        );

        Assert.Contains
        (
            "no non-key columns",
            ex.Message
        );
    }



    [Fact]
    public void BuildUpdate_when_key_properties_are_NotMapped_throws_InvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>
        (
            DbCommandBuilder.BuildUpdate<NotMappedKeyRecord>
        );

        Assert.Contains
        (
            "none are mapped",
            ex.Message
        );
    }
}
