namespace Domain.Entities;
using Domain.Common;
/// <summary>
/// Bien the san pham (size/color): sku, variant_name, attributes (jsonb), price, compare_at, cost, weight_gram, status.
/// </summary>
public class ProductVariant : SoftDeleteEntity<Guid>
{
    public Guid ProductId { get; set; }
    public required string Sku { get; set; }
    public string? VariantName { get; set; }
    /// <summary>VD: {"size":"M","color":"Red"}</summary>
    public string? Attributes { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAt { get; set; }
    public decimal? Cost { get; set; }
    public int? WeightGram { get; set; }
    public int Status { get; set; } = 1;

    public Product Product { get; set; } = null!;
}
