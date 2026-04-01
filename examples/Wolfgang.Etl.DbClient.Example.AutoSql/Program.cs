using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;
using Wolfgang.Etl.DbClient.Example.AutoSql;

// ---------------------------------------------------------------
// Example: Auto-generated SQL from [Table], [Column], [Key] attributes
// ---------------------------------------------------------------
// DbCommandBuilder generates SELECT, INSERT, and UPDATE statements
// automatically from data annotation attributes on the record type.

using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();

// Create the Products table
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = @"
        CREATE TABLE Products (
            product_id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            category TEXT NOT NULL,
            price REAL NOT NULL
        );";
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine("=== Auto-SQL Example: Extract and Load with attribute-based SQL ===");
Console.WriteLine();

// LOAD: Auto-generates INSERT INTO Products (name, category, price) VALUES (@Name, @Category, @Price)
// Note: the [Key] + [DatabaseGenerated(Identity)] column 'product_id' is excluded from INSERT.
var loader = new DbLoader<ProductRecord>(connection, WriteMode.Insert);
Console.WriteLine($"Loader SQL: {loader.CommandText}");
Console.WriteLine();

await loader.LoadAsync(SeedProducts());

static async IAsyncEnumerable<ProductRecord> SeedProducts()
{
    var products = new[]
    {
        new ProductRecord { Name = "Widget A", Category = "Widgets", Price = 9.99m },
        new ProductRecord { Name = "Widget B", Category = "Widgets", Price = 14.99m },
        new ProductRecord { Name = "Gadget X", Category = "Gadgets", Price = 29.99m },
        new ProductRecord { Name = "Gadget Y", Category = "Gadgets", Price = 49.99m },
        new ProductRecord { Name = "Doohickey", Category = "Misc", Price = 4.99m },
    };

    foreach (var p in products)
    {
        Console.WriteLine($"  Inserting: {p.Name} ({p.Category}) ${p.Price}");
        await Task.CompletedTask;
        yield return p;
    }
}

Console.WriteLine();

// EXTRACT: Auto-generates SELECT product_id AS Id, name AS Name, category AS Category, price AS Price FROM Products
var extractor = new DbExtractor<ProductRecord>(connection);
Console.WriteLine($"Extractor SQL: {extractor.CommandText}");
Console.WriteLine();

Console.WriteLine("All products:");
await foreach (var product in extractor.ExtractAsync())
{
    Console.WriteLine($"  [{product.Id}] {product.Name} ({product.Category}): ${product.Price}");
}
