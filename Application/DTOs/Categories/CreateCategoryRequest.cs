using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Categories;

/// <summary>
/// Request tao moi danh muc (REST POST body).
/// </summary>
public sealed class CreateCategoryRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string Slug { get; set; } = "";

    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
}
