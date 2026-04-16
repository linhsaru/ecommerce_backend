using Domain.Enums;

namespace Application.DTOs.Payments;

public sealed class CreateVietQrPaymentResponse
{
    public Guid OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string TransactionRef { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
    public string BankBin { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string TransferContent { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTimeOffset ExpireAt { get; set; }
}
