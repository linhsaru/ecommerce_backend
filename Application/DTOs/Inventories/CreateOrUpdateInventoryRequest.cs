using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Inventories;

public sealed class CreateOrUpdateInventoryRequest
{
    public Guid WarehouseId { get; set; }
    public Guid VariantId { get; set; }
    public int Quantity { get; set; }
    public int Reserved { get; set; }
}
