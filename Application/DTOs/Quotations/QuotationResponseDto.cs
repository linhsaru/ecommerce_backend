namespace Application.DTOs.Quotations;

//Dữ liệu đầy đủ cho một bản in báo giá
public sealed class QuotationResponseDto
{
    public CompanyInfoDto Company { get; init; } = new();
    public CustomerInfoDto Customer { get; init; } = new();
    public IReadOnlyList<QuotationItemDto> Items { get; init; } = Array.Empty<QuotationItemDto>();
    public QuotationSummaryDto Summary { get; init; } = new();
    //Ghi chú hiển thị phía dưới bảng
    public string Notes { get; init; } = "";
}
