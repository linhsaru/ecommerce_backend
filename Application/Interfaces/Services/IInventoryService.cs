using Application.Common;
using Application.DTOs.Inventories;

namespace Application.Interfaces.Services;

public interface IInventoryService
{
    Task<Result<(List<InventoryDto> Items, long Total)>> GetPagedAsync(
        int page,
        int pageSize,
        Guid? warehouseId,
        Guid? variantId,
        string? search,
        CancellationToken cancellationToken = default);

    Task<Result<List<WarehouseInventorySummaryDto>>> GetWarehouseSummariesAsync(CancellationToken cancellationToken = default);

    Task<Result<InventoryDto?>> GetByKeyAsync(Guid warehouseId, Guid variantId, CancellationToken cancellationToken = default);
    Task<Result<InventoryDto>> CreateOrUpdateAsync(CreateOrUpdateInventoryRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid warehouseId, Guid variantId, CancellationToken cancellationToken = default);
}
