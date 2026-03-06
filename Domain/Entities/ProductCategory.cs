namespace Domain.Entities;

/// <summary>
/// Bang trung gian many-to-many giua Product va Category.
/// </summary>
public class ProductCategory
{
    public long ProductId { get; set; }
    public long CategoryId { get; set; }

    public Product Product { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
