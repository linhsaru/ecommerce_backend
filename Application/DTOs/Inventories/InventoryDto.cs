namespace Application.DTOs.Inventories;

public sealed class InventoryDto
{
    public Guid WarehouseId { get; init; }
    public Guid VariantId { get; init; }
    public string? WarehouseName { get; init; }
    public string? VariantSku { get; init; }
    public int Quantity { get; init; }
    public int Reserved { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
