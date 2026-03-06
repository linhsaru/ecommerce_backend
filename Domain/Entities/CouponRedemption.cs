using System;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Lich su su dung coupon: coupon_id, user_id, order_id.
/// </summary>
public class CouponRedemption : BaseEntity<long>
{
    public long CouponId { get; set; }
    public long? UserId { get; set; }
    public long? OrderId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Coupon Coupon { get; set; } = null!;
    public User? User { get; set; }
    public Order? Order { get; set; }
}
