using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Payments;

public sealed class CreateVietQrPaymentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [EmailAddress]
    [MaxLength(255)]
    public string? RecipientEmail { get; set; }
}
