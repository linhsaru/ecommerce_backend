using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.DTOs.Payments;

public sealed class VietQrWebhookRequest
{
    [MaxLength(100)]
    public string TransactionRef { get; set; } = string.Empty;

    [Range(1, double.MaxValue)]
    public decimal? Amount { get; set; }

    [JsonPropertyName("code")]
    [MaxLength(64)]
    public string? Code { get; set; }

    [JsonPropertyName("content")]
    [MaxLength(2000)]
    public string? Content { get; set; }

    [JsonPropertyName("description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [JsonPropertyName("transferAmount")]
    [Range(1, double.MaxValue)]
    public decimal? TransferAmount { get; set; }

    [MaxLength(200)]
    public string? BankTransactionId { get; set; }
}
