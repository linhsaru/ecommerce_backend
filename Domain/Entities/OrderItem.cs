using System;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Dong don hang: snapshot sku, name, variant_name, unit_price, quantity, line_total.
/// </summary>
public class OrderItem : BaseEntity<Guid>
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public string? VariantName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Order Order { get; set; } = null!;
}
