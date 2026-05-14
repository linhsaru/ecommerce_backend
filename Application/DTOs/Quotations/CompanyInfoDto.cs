namespace Application.DTOs.Quotations;

//Thông tin công ty hiển thị trên báo giá
public sealed class CompanyInfoDto
{
    public string CompanyName { get; init; } = "";
    public string AddressLine1 { get; init; } = "";
    public string? AddressLine2 { get; init; }
    public string Hotline { get; init; } = "";
    public string Email { get; init; } = "";
    public string Website { get; init; } = "";
}
