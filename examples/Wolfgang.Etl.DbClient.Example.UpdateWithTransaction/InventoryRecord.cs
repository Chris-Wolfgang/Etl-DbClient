using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JetBrains.Annotations;

namespace Wolfgang.Etl.DbClient.Example.UpdateWithTransaction;

[Table("Inventory")]
[UsedImplicitly(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.WithMembers)]
public class InventoryRecord
{
    [Key]
    [Column("sku")]
    public string Sku { get; set; } = string.Empty;

    [Column("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Column("last_updated")]
    public string LastUpdated { get; set; } = string.Empty;
}
