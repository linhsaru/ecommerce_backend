using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Categories;

/// <summary>
/// Request cap nhat danh muc (REST PUT/PATCH body).
/// </summary>
public sealed class UpdateCategoryRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(200)]
    public string? Slug { get; set; }

    public Guid? ParentId { get; set; }
    public int? SortOrder { get; set; }
}
