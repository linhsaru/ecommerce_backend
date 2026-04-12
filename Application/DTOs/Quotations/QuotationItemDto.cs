namespace Application.DTOs.Quotations;

//Một dòng linh kiện / sản phẩm trong báo giá
public sealed class QuotationItemDto
{
    public int LineNumber { get; init; }
    public string ProductName { get; init; } = "";
    public string Warranty { get; init; } = "";
    public int Quantity { get; init; }
    //Đơn giá (VND), đã làm tròn.
    public decimal UnitPrice { get; init; }
    //Thành tiền = Đơn giá × Số lượng.
    public decimal LineTotal { get; init; }
}
