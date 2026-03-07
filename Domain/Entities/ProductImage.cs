namespace Domain.Entities;
using Domain.Common;
/// <summary>
/// Anh san pham: url, alt, sort_order.
/// </summary>
public class ProductImage : BaseEntity<Guid>
{
    public Guid ProductId { get; set; }
    public required string Url { get; set; }
    public string? Alt { get; set; }
    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;
}
