using System.Diagnostics.CodeAnalysis;

namespace Wolfgang.Etl.DbClient.Tests.Unit;

// "Total" is a textual prefix of "TotalTax" — used to regression-test the
// multi-row InsertBatchSize path against param-name collisions (#279).
[ExcludeFromCodeCoverage]
public class OrderRecord
{
    public decimal Total { get; set; }



    public decimal TotalTax { get; set; }
}
