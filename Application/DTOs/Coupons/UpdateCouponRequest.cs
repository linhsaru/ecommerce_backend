using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Coupons;

public sealed class UpdateCouponRequest
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(20)]
    public string? DiscountType { get; set; }

    public decimal? DiscountValue { get; set; }
    public decimal? MinOrderValue { get; set; }
    public decimal? MaxDiscount { get; set; }
    public int? UsageLimit { get; set; }
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public int? Status { get; set; }
}
