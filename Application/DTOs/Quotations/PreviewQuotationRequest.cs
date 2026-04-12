namespace Application.DTOs.Quotations;

//Dòng yêu cầu báo giá: sản phẩm + số lượng
public sealed class QuotationLineRequest
{
    public Guid ProductId { get; init; }
    public Guid? VariantId { get; init; }
    public int Quantity { get; init; } = 1;
}

//POST preview báo giá từ danh sách sản phẩm.
public sealed class PreviewQuotationRequest
{
    public List<QuotationLineRequest> Items { get; init; } = new();

    public PreviewQuotationCustomerRequest? Customer { get; init; }

    public decimal ShippingFee { get; init; }
    public decimal OtherCosts { get; init; }
    public decimal Discount { get; init; }

    //Nếu null, dùng ngày hiện tại
    public DateOnly? QuotationDate { get; init; }
}
