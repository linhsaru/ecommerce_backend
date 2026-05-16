namespace Application.DTOs.Inventories;

public sealed class WarehouseInventorySummaryDto
{
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = "";
    public int TotalQuantity { get; init; }
    public int TotalReserved { get; init; }
}
