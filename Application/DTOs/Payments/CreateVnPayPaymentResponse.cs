namespace Application.DTOs.Payments;

public sealed class CreateVnPayPaymentResponse
{
    public Guid OrderId { get; set; }
    public string TransactionRef { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public DateTimeOffset ExpireAt { get; set; }
}
