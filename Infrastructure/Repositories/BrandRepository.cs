using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class BrandRepository : IBrandRepository
{
    private readonly IAppDbContext _db;

    public BrandRepository(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<(Brand Brand, int ProductCount)>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Brands
            .AsNoTracking()
            .Where(b => b.DeletedAt == null)
            .Select(b => new
            {
                Brand = b,
                ProductCount = b.Products.Count(p => p.DeletedAt == null && p.Status == 1),
            })
            .OrderByDescending(x => x.ProductCount)
            .ThenBy(x => x.Brand.Name)
            .ToListAsync(cancellationToken);

        return rows.ConvertAll(x => (x.Brand, x.ProductCount));
    }

    public async Task<(Brand? Brand, int ProductCount)> GetBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.Brands
            .AsNoTracking()
            .Where(b => b.Slug == slug && b.DeletedAt == null)
            .Select(b => new
            {
                Brand = b,
                ProductCount = b.Products.Count(p => p.DeletedAt == null && p.Status == 1),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row == null ? (null, 0) : (row.Brand, row.ProductCount);
    }
}
