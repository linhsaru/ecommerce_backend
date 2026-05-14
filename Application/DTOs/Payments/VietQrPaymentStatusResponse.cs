using Domain.Enums;

namespace Application.DTOs.Payments;

public sealed class VietQrPaymentStatusResponse
{
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string TransactionRef { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public bool IsSuccess { get; set; }
}
