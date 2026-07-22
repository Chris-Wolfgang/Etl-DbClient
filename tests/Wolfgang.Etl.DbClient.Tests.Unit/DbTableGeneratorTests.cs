using Dapper;
using JetBrains.Annotations;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

// Test fixtures live OUTSIDE the test class so the source generator picks
// them up at compile time. The generator emits a partial with `public const
// string Insert` and `public static void Bind(DynamicParameters, TRecord)`.
//
// UsedImplicitly: source-generator-emitted code reads these properties via
// `record.FirstName` etc; the tests only verify the generated SQL string.
// ReSharper has no visibility into the generator's output at static-analysis
// time, so without this marker every fixture property looks unused.

[DbTable("people")]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
public partial record GeneratedPerson
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public int Age { get; init; }
}



[DbTable("orders")]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
public partial record GeneratedOrder
{
    [DbColumn("order_id")]
    public int OrderId { get; init; }

    [DbColumn("customer_name")]
    public string Customer { get; init; } = string.Empty;

    public decimal Total { get; init; }

    // Computed column the application doesn't write — verifies Skip=true
    // excludes the property from the generated SQL + binder.
    [DbColumn("audit", Skip = true)]
    public string Audit { get; init; } = string.Empty;
}



// Single-key fixture — verifies Update/Delete emit against a single
// [DbKey] property with a [DbColumn] override on the key column.
[DbTable("widgets")]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
public partial record GeneratedWidget
{
    [DbKey]
    [DbColumn("widget_id")]
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}



// Composite-key fixture — two [DbKey] properties in declaration order.
// The WHERE clause preserves that order.
[DbTable("order_lines")]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
public partial record GeneratedOrderLine
{
    [DbKey]
    [DbColumn("order_id")]
    public int OrderId { get; init; }

    [DbKey]
    [DbColumn("line_no")]
    public int LineNumber { get; init; }

    public string Sku { get; init; } = string.Empty;
    public int Quantity { get; init; }
}



// Key-only fixture — no non-key columns. Update MUST NOT be emitted
// (nothing to SET). Delete is still emitted.
[DbTable("tokens")]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
public partial record GeneratedTokenOnly
{
    [DbKey]
    public string Token { get; init; } = string.Empty;
}



public class DbTableGeneratorTests
{
    [Fact]
    public void Generator_emits_Insert_const_using_property_names()
    {
        var sql = GeneratedPerson.Insert;

        Assert.Equal
        (
            "INSERT INTO people (FirstName, LastName, Age) VALUES (@FirstName, @LastName, @Age)",
            sql
        );
    }



    [Fact]
    public void Generator_honors_DbColumn_name_override_and_Skip()
    {
        var sql = GeneratedOrder.Insert;

        // Audit is Skip=true → absent. OrderId and Customer carry their
        // DbColumn override names; Total uses the property name.
        Assert.Equal
        (
            "INSERT INTO orders (order_id, customer_name, Total) VALUES (@OrderId, @Customer, @Total)",
            sql
        );
    }



    [Fact]
    public void Generator_emits_Select_const_using_property_names()
    {
        var sql = GeneratedPerson.Select;

        // No [DbColumn] overrides on GeneratedPerson — column name equals
        // property name for every field, so BuildSelect's aliasing rule
        // (alias only on name mismatch) collapses to a plain column list.
        Assert.Equal
        (
            "SELECT FirstName, LastName, Age FROM people",
            sql
        );
    }



    [Fact]
    public void Generator_emits_Select_const_with_column_aliasing_when_names_differ()
    {
        var sql = GeneratedOrder.Select;

        // Audit is Skip=true → absent. OrderId + Customer carry [DbColumn]
        // overrides so BuildSelect aliases `col AS Property`. Total has
        // no override — matches property name, no alias.
        Assert.Equal
        (
            "SELECT order_id AS OrderId, customer_name AS Customer, Total FROM orders",
            sql
        );
    }



    [Fact]
    public void Generator_emits_Update_const_with_single_key_and_column_aliasing()
    {
        var sql = GeneratedWidget.Update;

        // SET covers every non-key column; WHERE uses the single [DbKey]
        // with its [DbColumn] override.
        Assert.Equal
        (
            "UPDATE widgets SET Name = @Name, Price = @Price WHERE widget_id = @Id",
            sql
        );
    }



    [Fact]
    public void Generator_emits_Update_const_with_composite_key_in_declaration_order()
    {
        var sql = GeneratedOrderLine.Update;

        Assert.Equal
        (
            "UPDATE order_lines SET Sku = @Sku, Quantity = @Quantity WHERE order_id = @OrderId AND line_no = @LineNumber",
            sql
        );
    }



    [Fact]
    public void Generator_emits_Delete_const_with_single_key()
    {
        var sql = GeneratedWidget.Delete;

        Assert.Equal
        (
            "DELETE FROM widgets WHERE widget_id = @Id",
            sql
        );
    }



    [Fact]
    public void Generator_emits_Delete_const_with_composite_key()
    {
        var sql = GeneratedOrderLine.Delete;

        Assert.Equal
        (
            "DELETE FROM order_lines WHERE order_id = @OrderId AND line_no = @LineNumber",
            sql
        );
    }



    [Fact]
    public void Generator_emits_Delete_but_not_Update_when_type_has_no_non_key_columns()
    {
        // Key-only type: Update would have nothing to SET, so it's not
        // emitted. Delete is still meaningful. Verified indirectly via
        // reflection — a compile-time reference to a missing const would
        // fail the build, so absence is what we assert.
        var deleteSql = GeneratedTokenOnly.Delete;
        Assert.Equal("DELETE FROM tokens WHERE Token = @Token", deleteSql);

        var hasUpdate = typeof(GeneratedTokenOnly).GetField
        (
            "Update",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        );
        Assert.Null(hasUpdate);
    }



    [Fact]
    public void Generator_does_not_emit_Update_or_Delete_when_type_has_no_key()
    {
        // GeneratedPerson / GeneratedOrder have no [DbKey] properties.
        // The Update/Delete consts must not be present — a Where clause
        // over zero keys would match every row.
        var updateField = typeof(GeneratedPerson).GetField
        (
            "Update",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        );
        var deleteField = typeof(GeneratedPerson).GetField
        (
            "Delete",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        );

        Assert.Null(updateField);
        Assert.Null(deleteField);
    }



    [Fact]
    public void Generator_emits_reflection_free_Bind_helper()
    {
        var p = new DynamicParameters();
        var record = new GeneratedPerson { FirstName = "Ada", LastName = "Lovelace", Age = 36 };

        GeneratedPerson.Bind(p, record);

        Assert.Equal("Ada", p.Get<string>("@FirstName"));
        Assert.Equal("Lovelace", p.Get<string>("@LastName"));
        Assert.Equal(36, p.Get<int>("@Age"));
    }
}
