using System.Linq;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Categories;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// Service CRUD danh muc: phan trang, tim theo slug/id, tao/cap nhat/xoa (soft delete).
/// </summary>
public sealed class CategoryService : ICategoryService
{
    private readonly IAppDbContext _db;

    public CategoryService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<(List<CategoryDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, long? parentId, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.AsNoTracking().Where(c => c.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Slug.Contains(search));
        if (parentId.HasValue)
            query = query.Where(c => c.ParentId == parentId.Value);

        var total = await query.LongCountAsync(cancellationToken);
        var skip = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100);
        var items = await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Skip((int)skip)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                ParentId = c.ParentId,
                Name = c.Name,
                Slug = c.Slug,
                SortOrder = c.SortOrder,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<(List<CategoryDto> Items, long Total)>.Ok((items, total));
    }

    public async Task<Result<CategoryDto?>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var cat = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == slug && c.DeletedAt == null, cancellationToken);
        if (cat == null)
            return Result<CategoryDto?>.Fail("NOT_FOUND", "Category not found.");
        return Result<CategoryDto?>.Ok(Map(cat));
    }

    public async Task<Result<CategoryDto?>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var cat = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken);
        if (cat == null)
            return Result<CategoryDto?>.Fail("NOT_FOUND", "Category not found.");
        return Result<CategoryDto?>.Ok(Map(cat));
    }

    public async Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Categories.AnyAsync(c => c.Slug == request.Slug && c.DeletedAt == null, cancellationToken);
        if (exists)
            return Result<CategoryDto>.Fail("VALIDATION_ERROR", "Slug already exists.");

        if (request.ParentId.HasValue)
        {
            var parentExists = await _db.Categories.AnyAsync(c => c.Id == request.ParentId && c.DeletedAt == null, cancellationToken);
            if (!parentExists)
                return Result<CategoryDto>.Fail("VALIDATION_ERROR", "Parent category not found.");
        }

        var category = new Category
        {
            Name = request.Name,
            Slug = request.Slug,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<CategoryDto>.Ok(Map(category));
    }

    public async Task<Result<CategoryDto>> UpdateAsync(long id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken);
        if (category == null)
            return Result<CategoryDto>.Fail("NOT_FOUND", "Category not found.");

        if (request.Name != null) category.Name = request.Name;
        if (request.Slug != null)
        {
            var exists = await _db.Categories.AnyAsync(c => c.Slug == request.Slug && c.Id != id && c.DeletedAt == null, cancellationToken);
            if (exists)
                return Result<CategoryDto>.Fail("VALIDATION_ERROR", "Slug already exists.");
            category.Slug = request.Slug;
        }
        if (request.ParentId.HasValue) category.ParentId = request.ParentId;
        if (request.SortOrder.HasValue) category.SortOrder = request.SortOrder.Value;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result<CategoryDto>.Ok(Map(category));
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken);
        if (category == null)
            return Result.Fail("NOT_FOUND", "Category not found.");
        category.MarkDeleted(null);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static CategoryDto Map(Category c)
    {
        return new CategoryDto
        {
            Id = c.Id,
            ParentId = c.ParentId,
            Name = c.Name,
            Slug = c.Slug,
            SortOrder = c.SortOrder,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }
}
