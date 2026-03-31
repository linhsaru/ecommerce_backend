namespace Application.DTOs.Coupons;

public sealed class CouponDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string? Name { get; init; }
    public string DiscountType { get; init; } = "";
    public decimal DiscountValue { get; init; }
    public decimal MinOrderValue { get; init; }
    public decimal? MaxDiscount { get; init; }
    public int? UsageLimit { get; init; }
    public int UsageCount { get; init; }
    public DateTimeOffset? StartAt { get; init; }
    public DateTimeOffset? EndAt { get; init; }
    public int Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
