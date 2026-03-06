using System;

namespace Application.DTOs.Categories;

/// <summary>
/// DTO tra ve thong tin danh muc.
/// </summary>
public sealed class CategoryDto
{
    public long Id { get; init; }
    public long? ParentId { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public int SortOrder { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
