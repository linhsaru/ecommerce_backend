namespace Domain.Entities;

/// <summary>
/// Bang trung gian many-to-many giua Product va Category.
/// </summary>
public class ProductCategory
{
    public Guid ProductId { get; set; }
    public Guid CategoryId { get; set; }

    public Product Product { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
