using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Payments;

public sealed class CreateVnPayPaymentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [MaxLength(250)]
    public string? OrderDescription { get; set; }

    [MaxLength(500)]
    public string? ReturnUrl { get; set; }
}
