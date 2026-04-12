namespace Application.DTOs.Quotations;

//Thông tin khách hàng trên báo giá
public sealed class CustomerInfoDto
{
    public string CustomerName { get; init; } = "";
    public string Address { get; init; } = "";
    public string? TaxCode { get; init; }
    //Ngày báo giá (mặc định: ngày hiện tại theo server)
    public DateOnly QuotationDate { get; init; }
}
