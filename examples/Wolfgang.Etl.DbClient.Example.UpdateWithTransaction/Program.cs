using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Wolfgang.Etl.DbClient;
using Wolfgang.Etl.DbClient.Example.UpdateWithTransaction;

// ---------------------------------------------------------------
// Example: UPDATE mode with caller-managed transaction
// ---------------------------------------------------------------
// This example shows:
// 1. Auto-generated UPDATE from [Key] attributes
// 2. Caller-managed transaction for coordinated multi-step operations
// 3. Progress reporting during loading

using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync().ConfigureAwait(false);

// Create and seed the Inventory table
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = @"
        CREATE TABLE Inventory (
            sku TEXT PRIMARY KEY,
            product_name TEXT NOT NULL,
            quantity INTEGER NOT NULL,
            last_updated TEXT NOT NULL
        );
        INSERT INTO Inventory VALUES ('SKU-001', 'Widget A', 100, '2026-01-01');
        INSERT INTO Inventory VALUES ('SKU-002', 'Widget B',  50, '2026-01-01');
        INSERT INTO Inventory VALUES ('SKU-003', 'Gadget X',  75, '2026-01-01');
        INSERT INTO Inventory VALUES ('SKU-004', 'Gadget Y',  25, '2026-01-01');
        INSERT INTO Inventory VALUES ('SKU-005', 'Doohickey', 200, '2026-01-01');";
    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
}

Console.WriteLine("=== Update with Transaction Example ===");
Console.WriteLine();

// Show current state
Console.WriteLine("Before update:");
await PrintInventoryAsync(connection).ConfigureAwait(false);

// Create a caller-managed transaction
using var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

// Auto-generates:
// UPDATE Inventory SET product_name = @ProductName, quantity = @Quantity, last_updated = @LastUpdated
// WHERE sku = @Sku
var loader = new DbLoader<InventoryRecord>(
    connection,
    WriteMode.Update,
    transaction
);

Console.WriteLine($"Update SQL: {loader.CommandText}");
Console.WriteLine();

// Apply inventory adjustments within the caller's transaction
await loader.LoadAsync(GetAdjustmentsAsync()).ConfigureAwait(false);

// Commit the transaction (caller controls the lifetime)
await transaction.CommitAsync().ConfigureAwait(false);

Console.WriteLine();
Console.WriteLine("After update:");
await PrintInventoryAsync(connection).ConfigureAwait(false);

static async IAsyncEnumerable<InventoryRecord> GetAdjustmentsAsync()
{
    var adjustments = new[]
    {
        new InventoryRecord { Sku = "SKU-001", ProductName = "Widget A", Quantity = 85, LastUpdated = "2026-03-29" },
        new InventoryRecord { Sku = "SKU-003", ProductName = "Gadget X", Quantity = 60, LastUpdated = "2026-03-29" },
        new InventoryRecord { Sku = "SKU-005", ProductName = "Doohickey", Quantity = 180, LastUpdated = "2026-03-29" },
    };

    foreach (var adj in adjustments)
    {
        Console.WriteLine($"  Updating {adj.Sku}: quantity {adj.Quantity}");
        await Task.CompletedTask.ConfigureAwait(false);
        yield return adj;
    }
}

static async Task PrintInventoryAsync(SqliteConnection conn)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT sku, product_name, quantity, last_updated FROM Inventory ORDER BY sku";
    using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
    while (await reader.ReadAsync().ConfigureAwait(false))
    {
        Console.WriteLine($"  {reader.GetString(0)}: {reader.GetString(1)} — qty {reader.GetInt32(2)} (updated {reader.GetString(3)})");
    }

    Console.WriteLine();
}
