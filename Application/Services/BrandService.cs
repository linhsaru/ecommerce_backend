using Application.DTOs.Brands;
using Application.Interfaces.Services;
using Domain.Interfaces.Repositories;

namespace Application.Services;

public class BrandService : IBrandService
{
    private readonly IBrandRepository _brandRpository;

    public BrandService(IBrandRepository brandRpository) => _brandRpository = brandRpository;

    public async Task<IEnumerable<BrandDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _brandRpository.GetAllAsync(cancellationToken);
        return rows.Select(tuple => new BrandDto
        {
            Id = tuple.Brand.Id,
            Name = tuple.Brand.Name,
            Slug = tuple.Brand.Slug,
            ProductCount = tuple.ProductCount,
        });
    }

    public async Task<BrandDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var (brand, productCount) = await _brandRpository.GetBySlugAsync(slug, cancellationToken);
        if (brand == null) return null;
        return new BrandDto
        {
            Id = brand.Id,
            Name = brand.Name,
            Slug = brand.Slug,
            ProductCount = productCount,
        };
    }
}
