using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface ICouponRepository
{
    IQueryable<Coupon> GetQueryable();
    Task<Coupon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    void Add(Coupon coupon);
    void Update(Coupon coupon);
    void Remove(Coupon coupon);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
