namespace Application.DTOs.Quotations;

//Tổng hợp tiền trên báo giá
public sealed class QuotationSummaryDto
{
    //Tổng thành tiền các dòng (trước phí / giảm).
    public decimal SubTotal { get; init; }
    public decimal ShippingFee { get; init; }
    public decimal OtherCosts { get; init; }
    public decimal Discount { get; init; }
    //Tổng tiền đơn hàng = SubTotal + ShippingFee + OtherCosts - Discount.
    public decimal OrderTotal { get; init; }
}
