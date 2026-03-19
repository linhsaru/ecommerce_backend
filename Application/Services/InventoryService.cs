using Application.Common;
using Application.DTOs.Inventories;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repo;

    public InventoryService(IInventoryRepository repo) => _repo = repo;

    public async Task<Result<(List<InventoryDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, Guid? warehouseId, Guid? variantId, CancellationToken cancellationToken = default)
    {
        IQueryable<Inventory> query = _repo.GetQueryable()
            .Include(i => i.Warehouse)
            .Include(i => i.Variant);

        if (warehouseId.HasValue)
            query = query.Where(i => i.WarehouseId == warehouseId.Value);
        if (variantId.HasValue)
            query = query.Where(i => i.VariantId == variantId.Value);

        var total = await query.LongCountAsync(cancellationToken);
        var skip = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100);
        var items = await query
            .OrderBy(i => i.WarehouseId).ThenBy(i => i.VariantId)
            .Skip((int)skip)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(i => new InventoryDto
            {
                WarehouseId = i.WarehouseId,
                VariantId = i.VariantId,
                WarehouseName = i.Warehouse.Name,
                VariantSku = i.Variant.Sku,
                Quantity = i.Quantity,
                Reserved = i.Reserved,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<(List<InventoryDto> Items, long Total)>.Ok((items, total));
    }

    public async Task<Result<InventoryDto?>> GetByKeyAsync(Guid warehouseId, Guid variantId, CancellationToken cancellationToken = default)
    {
        var inv = await _repo.GetByKeyAsync(warehouseId, variantId, cancellationToken);
        if (inv == null)
            return Result<InventoryDto?>.Fail("NOT_FOUND", "Inventory not found.");
        return Result<InventoryDto?>.Ok(Map(inv));
    }

    public async Task<Result<InventoryDto>> CreateOrUpdateAsync(CreateOrUpdateInventoryRequest request, CancellationToken cancellationToken = default)
    {
        var inv = await _repo.GetByKeyAsync(request.WarehouseId, request.VariantId, cancellationToken);
        if (inv != null)
        {
            inv.Quantity = request.Quantity;
            inv.Reserved = request.Reserved;
            inv.UpdatedAt = DateTimeOffset.UtcNow;
            _repo.Update(inv);
        }
        else
        {
            inv = new Inventory
            {
                WarehouseId = request.WarehouseId,
                VariantId = request.VariantId,
                Quantity = request.Quantity,
                Reserved = request.Reserved,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _repo.Add(inv);
        }
        await _repo.SaveChangesAsync(cancellationToken);

        var updated = await _repo.GetByKeyAsync(request.WarehouseId, request.VariantId, cancellationToken);
        return Result<InventoryDto>.Ok(Map(updated!));
    }

    public async Task<Result> DeleteAsync(Guid warehouseId, Guid variantId, CancellationToken cancellationToken = default)
    {
        var inv = await _repo.GetByKeyAsync(warehouseId, variantId, cancellationToken);
        if (inv == null)
            return Result.Fail("NOT_FOUND", "Inventory not found.");
        _repo.Remove(inv);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static InventoryDto Map(Inventory i) => new()
    {
        WarehouseId = i.WarehouseId,
        VariantId = i.VariantId,
        WarehouseName = i.Warehouse?.Name,
        VariantSku = i.Variant?.Sku,
        Quantity = i.Quantity,
        Reserved = i.Reserved,
        UpdatedAt = i.UpdatedAt
    };
}
