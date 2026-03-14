using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class CouponRepository : ICouponRepository
{
    private readonly IAppDbContext _db;

    public CouponRepository(IAppDbContext db) => _db = db;

    public IQueryable<Coupon> GetQueryable() => _db.Coupons.AsNoTracking();

    public async Task<Coupon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        => await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code, cancellationToken);

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await _db.Coupons.AnyAsync(c => c.Code == code && (excludeId == null || c.Id != excludeId.Value), cancellationToken);

    public void Add(Coupon coupon) => _db.Coupons.Add(coupon);
    public void Update(Coupon coupon) => _db.Coupons.Update(coupon);
    public void Remove(Coupon coupon) => _db.Coupons.Remove(coupon);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
