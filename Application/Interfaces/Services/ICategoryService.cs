using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Categories;

namespace Application.Interfaces.Services;

/// <summary>
/// Service CRUD danh muc theo chuan REST API.
/// </summary>
public interface ICategoryService
{
    Task<Result<List<CategoryDto>>> GetListAsync(string? search, Guid? parentId, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
