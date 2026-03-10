using System.Linq;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Categories;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// Service CRUD danh muc: phan trang, tim theo slug/id, tao/cap nhat/xoa (soft delete).
/// </summary>
public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;

    public CategoryService(ICategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<(List<CategoryDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, long? parentId, CancellationToken cancellationToken = default)
    {
        var query = _repository.GetQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Slug.Contains(search));
        if (parentId.HasValue)
            query = query.Where(c => c.ParentId.Equals(parentId.Value));

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
        var cat = await _repository.GetBySlugAsync(slug, cancellationToken);
        if (cat == null)
        {
            return Result<CategoryDto?>.Fail("NOT_FOUND", "Category not found!");
        }
        return Result<CategoryDto?>.Ok(Map(cat));
    }

    public async Task<Result<CategoryDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cat = await _repository.GetByIdAsync(id, cancellationToken);
        if (cat == null)
            return Result<CategoryDto?>.Fail("NOT_FOUND", "Category not found.");
        return Result<CategoryDto?>.Ok(Map(cat));
    }

    public async Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (await _repository.ExistsBySlugAsync(request.Slug, null, cancellationToken))
            return Result<CategoryDto>.Fail("VALIDATION_ERROR", "Slug already exists.");

        if (request.ParentId.HasValue && !await _repository.ExistsByIdAsync(request.ParentId.Value, cancellationToken))
            return Result<CategoryDto>.Fail("VALIDATION_ERROR", "Parent category not found.");

        var category = new Category
        {
            Name = request.Name,
            Slug = request.Slug,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder
        };

        _repository.Add(category);
        await _repository.SaveChangesAsync(cancellationToken); // Repo chịu trách nhiệm Save

        return Result<CategoryDto>.Ok(Map(category));
    }

    public async Task<Result<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken);
        if (category == null)
            return Result<CategoryDto>.Fail("NOT_FOUND", "Category not found.");

        if (request.Slug != null && await _repository.ExistsBySlugAsync(request.Slug, id, cancellationToken))
            return Result<CategoryDto>.Fail("VALIDATION_ERROR", "Slug already exists.");

        if (request.Name != null) category.Name = request.Name;
        if (request.Slug != null) category.Slug = request.Slug;
        if (request.ParentId.HasValue) category.ParentId = request.ParentId;
        if (request.SortOrder.HasValue) category.SortOrder = request.SortOrder.Value;

        category.UpdatedAt = DateTimeOffset.UtcNow;

        _repository.Update(category);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result<CategoryDto>.Ok(Map(category));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _repository.GetByIdAsync(id, cancellationToken);
        if (category == null)
            return Result.Fail("NOT_FOUND", "Category not found.");

        category.MarkDeleted(null);

        _repository.Update(category);
        await _repository.SaveChangesAsync(cancellationToken);

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
