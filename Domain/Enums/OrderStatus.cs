namespace Domain.Enums;

/// <summary>
/// Trang thai don hang: pending, confirmed, processing, shipping, completed, cancelled, refunded.
/// </summary>
public enum OrderStatus
{
    pending = 0,
    confirmed = 1,
    processing = 2,
    shipping = 3,
    completed = 4,
    cancelled = 5,
    refunded = 6
}
