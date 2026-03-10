using System;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Ma giam gia: code, discount_type (percent|fixed), discount_value, min_order_value, max_discount, usage_limit, start_at, end_at, status.
/// </summary>
public class Coupon : BaseEntity<Guid>
{
    public required string Code { get; set; }
    public string? Name { get; set; }
    public required string DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal MinOrderValue { get; set; }
    public decimal? MaxDiscount { get; set; }
    public int? UsageLimit { get; set; }
    public int UsageCount { get; set; }
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public int Status { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
