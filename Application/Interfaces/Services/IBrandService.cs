using Application.DTOs.Brands;

namespace Application.Interfaces.Services;

public interface IBrandService
{
    Task<IEnumerable<BrandDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BrandDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
