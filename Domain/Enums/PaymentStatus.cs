namespace Domain.Enums;

/// <summary>
/// Trang thai thanh toan: unpaid, paid, failed, refunded, partially_refunded.
/// </summary>
public enum PaymentStatus
{
    unpaid = 0,
    paid = 1,
    failed = 2,
    refunded = 3,
    partially_refunded = 4
}
