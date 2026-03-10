using System;

namespace Domain.Entities;

/// <summary>
/// Muc trong gio hang: cart_id, variant_id, quantity.
/// </summary>
public class CartItem
{
    public Guid CartId { get; set; }
    public long VariantId { get; set; }
    public int Quantity { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Cart Cart { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
