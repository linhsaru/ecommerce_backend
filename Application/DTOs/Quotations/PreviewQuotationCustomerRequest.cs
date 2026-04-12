namespace Application.DTOs.Quotations;

//Thông tin khách tùy chọn khi gọi preview báo giá
public sealed class PreviewQuotationCustomerRequest
{
    public string? CustomerName { get; init; }
    public string? Address { get; init; }
    public string? TaxCode { get; init; }
}
