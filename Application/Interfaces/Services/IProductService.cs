using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Products;

namespace Application.Interfaces.Services;

/// <summary>
/// Service CRUD san pham theo chuan REST API.
/// </summary>
public interface IProductService
{
    Task<Result<(List<ProductDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, int? status, List<Guid>? categoryId, string? categorySlug, CancellationToken cancellationToken = default);
    Task<Result<ProductDetailDto?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<ProductDetailDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
