namespace Domain.Entities;
using Domain.Common;
/// <summary>
/// Dia chi giao hang cua user: recipient, phone, line1, line2, ward, district, province, country.
/// </summary>
public class UserAddress : SoftDeleteEntity<Guid>
{
    public Guid UserId { get; set; }
    public required string Recipient { get; set; }
    public required string Phone { get; set; }
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? Ward { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string Country { get; set; } = "VN";
    public string? PostalCode { get; set; }
    public bool IsDefault { get; set; }

    public User User { get; set; } = null!;
}
