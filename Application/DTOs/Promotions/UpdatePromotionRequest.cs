using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Promotions;

public sealed class UpdatePromotionRequest
{
    [MaxLength(255)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [MaxLength(500)]
    public string? BannerImage { get; set; }

    [MaxLength(50)]
    public string? DiscountType { get; set; }

    public decimal? DiscountValue { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }
}
