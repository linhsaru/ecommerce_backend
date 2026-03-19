using Domain.Entities;

namespace Domain.Interfaces.Repositories
{
    public interface IInventoryRepository
    {
        IQueryable<Inventory> GetQueryable();
        Task<Inventory?> GetByKeyAsync(Guid warehouseId, Guid variantId, CancellationToken ct = default);
        Task<int> GetTotalStockAsync(Guid variantId, CancellationToken ct = default);
        void Add(Inventory inventory);
        void Update(Inventory inventory);
        void Remove(Inventory inventory);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
