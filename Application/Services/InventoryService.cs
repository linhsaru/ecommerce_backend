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

    public async Task<Result<(List<InventoryDto> Items, long Total)>> GetPagedAsync(
        int page,
        int pageSize,
        Guid? warehouseId,
        Guid? variantId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (Math.Max(1, page) - 1) * normalizedPageSize;

        var query = _repo.GetQueryable()
            .Where(i =>
                i.Variant.DeletedAt == null &&
                i.Variant.Product.DeletedAt == null);

        if (warehouseId.HasValue)
            query = query.Where(i => i.WarehouseId == warehouseId.Value);

        if (variantId.HasValue)
            query = query.Where(i => i.VariantId == variantId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(i =>
                i.Variant.Product.Name.Contains(term) ||
                (i.Warehouse.Name != null && i.Warehouse.Name.Contains(term)));
        }

        query = query
            .OrderBy(i => i.Warehouse.Name)
            .ThenBy(i => i.Variant.Product.Name)
            .ThenByDescending(i => i.UpdatedAt);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip((int)skip)
            .Take(normalizedPageSize)
            .Select(i => new InventoryDto
            {
                WarehouseId = i.WarehouseId,
                VariantId = i.VariantId,
                ProductId = i.Variant.ProductId,
                WarehouseName = i.Warehouse.Name,
                ProductName = i.Variant.Product.Name,
                VariantLabel = i.Variant.VariantName,
                Quantity = i.Quantity,
                Reserved = i.Reserved,
                UpdatedAt = i.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return Result<(List<InventoryDto> Items, long Total)>.Ok((items, total));
    }

    public async Task<Result<List<WarehouseInventorySummaryDto>>> GetWarehouseSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await _repo.GetQueryable()
            .Where(i =>
                i.Variant.DeletedAt == null &&
                i.Variant.Product.DeletedAt == null)
            .GroupBy(i => new { i.WarehouseId, i.Warehouse.Name })
            .Select(g => new WarehouseInventorySummaryDto
            {
                WarehouseId = g.Key.WarehouseId,
                WarehouseName = g.Key.Name,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalReserved = g.Sum(x => x.Reserved),
            })
            .OrderByDescending(x => x.TotalQuantity)
            .ThenBy(x => x.WarehouseName)
            .ToListAsync(cancellationToken);

        return Result<List<WarehouseInventorySummaryDto>>.Ok(list);
    }

    public async Task<Result<InventoryDto?>> GetByKeyAsync(
        Guid warehouseId,
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        var inv = await _repo.GetByKeyAsync(warehouseId, variantId, cancellationToken);
        if (inv == null)
            return Result<InventoryDto?>.Fail("NOT_FOUND", "Inventory not found.");
        return Result<InventoryDto?>.Ok(Map(inv));
    }

    public async Task<Result<InventoryDto>> CreateOrUpdateAsync(
        CreateOrUpdateInventoryRequest request,
        CancellationToken cancellationToken = default)
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
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _repo.Add(inv);
        }

        await _repo.SaveChangesAsync(cancellationToken);

        var updated = await _repo.GetByKeyAsync(request.WarehouseId, request.VariantId, cancellationToken);
        return Result<InventoryDto>.Ok(Map(updated!));
    }

    public async Task<Result> DeleteAsync(
        Guid warehouseId,
        Guid variantId,
        CancellationToken cancellationToken = default)
    {
        var inv = await _repo.GetByKeyAsync(warehouseId, variantId, cancellationToken);
        if (inv == null)
            return Result.Fail("NOT_FOUND", "Inventory not found.");
        _repo.Remove(inv);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static InventoryDto Map(Inventory i)
    {
        var product = i.Variant?.Product;
        var variant = i.Variant;

        return new InventoryDto
        {
            WarehouseId = i.WarehouseId,
            VariantId = i.VariantId,
            ProductId = variant?.ProductId ?? Guid.Empty,
            WarehouseName = i.Warehouse?.Name,
            ProductName = product?.Name ?? "",
            VariantLabel = variant?.VariantName,
            Quantity = i.Quantity,
            Reserved = i.Reserved,
            UpdatedAt = i.UpdatedAt,
        };
    }
}
