using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly IAppDbContext _db;

        public InventoryRepository(IAppDbContext db) => _db = db;

        public IQueryable<Inventory> GetQueryable() => _db.Inventories.AsNoTracking();

        public async Task<Inventory?> GetByKeyAsync(Guid warehouseId, Guid variantId, CancellationToken ct = default)
            => await _db.Inventories
                .Include(i => i.Warehouse)
                .Include(i => i.Variant)
                    .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(i => i.WarehouseId == warehouseId && i.VariantId == variantId, ct);

        public async Task<int> GetTotalStockAsync(Guid variantId, CancellationToken ct = default)
            => await _db.Inventories.Where(i => i.VariantId == variantId).SumAsync(i => i.Quantity, ct);

        public void Add(Inventory inventory) => _db.Inventories.Add(inventory);
        public void Update(Inventory inventory) => _db.Inventories.Update(inventory);
        public void Remove(Inventory inventory) => _db.Inventories.Remove(inventory);
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
