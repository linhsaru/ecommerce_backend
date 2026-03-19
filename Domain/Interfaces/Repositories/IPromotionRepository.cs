using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IPromotionRepository
{
    IQueryable<Promotion> GetQueryable();
    Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Promotion promotion);
    void Update(Promotion promotion);
    void Remove(Promotion promotion);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
