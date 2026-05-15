namespace Application.DTOs.Inventories;

public sealed class InventoryDto
{
    public Guid WarehouseId { get; init; }
    public Guid VariantId { get; init; }
    public Guid ProductId { get; init; }
    public string? WarehouseName { get; init; }
    /// <summary>Tên sản phẩm (từ biến thể).</summary>
    public string ProductName { get; init; } = "";
    /// <summary>Tên phân nhánh hiển thị (không dùng SKU).</summary>
    public string? VariantLabel { get; init; }
    public int Quantity { get; init; }
    public int Reserved { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
