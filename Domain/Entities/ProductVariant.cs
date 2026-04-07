namespace Domain.Entities;
using Domain.Common;
/// <summary>
/// Bien the san pham (size/color): sku, variant_name, attributes (jsonb), price, compare_at, cost, weight_gram, status.
/// </summary>
public class ProductVariant : SoftDeleteEntity<Guid>
{
    public Guid ProductId { get; set; }
    public required string Sku { get; set; } //Stock keeping unit - mã định danh của biến thể
    public string? VariantName { get; set; }
    /// <summary>VD: {"size":"M","color":"Red"}</summary>
    public string? Attributes { get; set; }
    public decimal Price { get; set; } //Giá bán hiện tại của biến thể
    public decimal? CompareAt { get; set; } //Giá gốc của biến thể
    public decimal? Cost { get; set; } //Giá nhập của biến thể
    public int? WeightGram { get; set; }
    public int Status { get; set; } = 1;

    public Product Product { get; set; } = null!;

    public ICollection<ProductVariantSpecification> ProductVariantSpecifications { get; set; } = new List<ProductVariantSpecification>();
    public ICollection<Component> Components { get; set; } = new List<Component>();
}
