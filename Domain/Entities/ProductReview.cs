using System;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Danh gia san pham: product_id, user_id, rating (1-5), title, content, status.
/// </summary>
public class ProductReview : BaseEntity<long>
{
    public long ProductId { get; set; }
    public long? UserId { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public int Status { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Product Product { get; set; } = null!;
    public User? User { get; set; }
}
