namespace Application.DTOs.Promotions;

public sealed class PromotionDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string? BannerImage { get; init; }
    public string? DiscountType { get; init; }
    public decimal DiscountValue { get; init; }
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
