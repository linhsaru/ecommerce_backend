using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class PromotionRepository : IPromotionRepository
{
    private readonly IAppDbContext _db;

    public PromotionRepository(IAppDbContext db) => _db = db;

    public IQueryable<Promotion> GetQueryable() => _db.Promotions.AsNoTracking();

    public async Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _db.Promotions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(Promotion promotion) => _db.Promotions.Add(promotion);
    public void Update(Promotion promotion) => _db.Promotions.Update(promotion);
    public void Remove(Promotion promotion) => _db.Promotions.Remove(promotion);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
