using System;

namespace Domain.Entities;

/// <summary>
/// Ton kho: warehouse_id, variant_id, quantity, reserved.
/// </summary>
public class Inventory
{
    public long WarehouseId { get; set; }
    public long VariantId { get; set; }
    public int Quantity { get; set; }
    public int Reserved { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Warehouse Warehouse { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
