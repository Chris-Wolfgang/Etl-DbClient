using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

public class TableAttributeSchemaTests
{
    [ExcludeFromCodeCoverage]
    [UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
    [Table("Orders", Schema = "sales")]
    private sealed class SalesOrder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string Customer { get; set; } = string.Empty;
    }



    [Fact]
    public void BuildSelect_when_Table_has_a_Schema_qualifies_the_table_name()
    {
        Assert.EndsWith("FROM sales.Orders", DbCommandBuilder.BuildSelect<SalesOrder>(), System.StringComparison.Ordinal);
    }



    [Fact]
    public void BuildInsert_when_Table_has_a_Schema_qualifies_the_table_name()
    {
        Assert.StartsWith("INSERT INTO sales.Orders (", DbCommandBuilder.BuildInsert<SalesOrder>(), System.StringComparison.Ordinal);
    }



    [Fact]
    public void BuildUpdate_when_Table_has_a_Schema_qualifies_the_table_name()
    {
        Assert.StartsWith("UPDATE sales.Orders SET ", DbCommandBuilder.BuildUpdate<SalesOrder>(), System.StringComparison.Ordinal);
    }



    [Fact]
    public void BuildSelect_when_Table_has_no_Schema_uses_the_bare_table_name()
    {
        Assert.Equal("Customers", DbCommandBuilder.QualifyTableName(new TableAttribute("Customers")));
    }



    [Fact]
    public void Validate_when_Table_has_a_Schema_probes_the_schema_qualified_table()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "ATTACH DATABASE ':memory:' AS sales; CREATE TABLE Orders (Id INTEGER); CREATE TABLE sales.Orders (Id INTEGER, Customer TEXT);";
        cmd.ExecuteNonQuery();

        DbSchemaValidator.Validate<SalesOrder>(conn);
    }
}
