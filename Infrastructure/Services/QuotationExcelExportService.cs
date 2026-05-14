using Application.Common;
using Application.DTOs.Quotations;
using Application.Interfaces.Services;
using ClosedXML.Excel;

namespace Infrastructure.Services;

//Xuất file Excel báo giá linh kiện (ClosedXML)
public sealed class QuotationExcelExportService : IQuotationExcelExportService
{
    private const string MoneyFormat = "#,##0";
    private const string SheetName = "Báo giá";

    private static readonly string CustomerNoticeText =
        "Quý khách lưu ý: Giá bán, khuyến mại của sản phẩm có thể thay đổi theo thời điểm nhập hàng và chương trình khuyến mãi. " +
        "Vui lòng xác nhận lại giá, khuyến mại và tồn kho trước khi đặt cọc / thanh toán.";

    private readonly IQuotationService _quotationService;

    public QuotationExcelExportService(IQuotationService quotationService)
    {
        _quotationService = quotationService;
    }

    public async Task<Result<byte[]>> ExportAsync(PreviewQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await _quotationService.PreviewAsync(request, cancellationToken);
        if (preview.IsFailure || preview.Value is null)
            return Result<byte[]>.Fail(preview.Errors.ToArray());

        try
        {
            var bytes = BuildWorkbook(preview.Value);
            return Result<byte[]>.Ok(bytes);
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Fail("EXPORT_ERROR", "Không tạo được file Excel.", ex.Message);
        }
    }

    private static byte[] BuildWorkbook(QuotationResponseDto q)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(SheetName);

        ws.Style.Font.FontName = "Arial";
        ws.Style.Font.FontSize = 10;

        const int colB = 2;
        const int colC = 3;
        const int colD = 4;
        const int colE = 5;
        const int colF = 6;
        const int colG = 7;

        var company = q.Company;
        var customer = q.Customer;
        var items = q.Items.ToList();
        var summary = q.Summary;

        // --- Dòng 3–10: Thông tin công ty (cột B) ---
        ws.Cell(3, colB).Value = company.CompanyName;
        ws.Cell(3, colB).Style.Font.Bold = true;
        ws.Cell(3, colB).Style.Font.FontSize = 12;

        ws.Cell(4, colB).Value = company.AddressLine1;
        if (!string.IsNullOrWhiteSpace(company.AddressLine2))
            ws.Cell(5, colB).Value = company.AddressLine2;
        ws.Cell(6, colB).Value = $"Hotline: {company.Hotline}";
        ws.Cell(7, colB).Value = $"Email: {company.Email}";
        ws.Cell(8, colB).Value = $"Website: {company.Website}";

        // Dòng 11: Tiêu đề
        var titleRange = ws.Range(11, colB, 11, colG);
        titleRange.Merge();
        titleRange.FirstCell().Value = "BÁO GIÁ CHI TIẾT";
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 16;

        // Dòng 13–15: Khách hàng
        ws.Cell(13, colB).Value = "Tên khách hàng:";
        ws.Cell(13, colC).Value = customer.CustomerName;
        ws.Cell(13, colE).Value = "Ngày:";
        ws.Cell(13, colF).Value = customer.QuotationDate.ToString("dd/MM/yyyy");

        ws.Cell(14, colB).Value = "Địa chỉ:";
        ws.Cell(14, colC).Value = customer.Address;
        ws.Cell(14, colE).Value = "Đơn vị tiền:";
        ws.Cell(14, colF).Value = "VNĐ";

        ws.Cell(15, colB).Value = "Mã số thuế:";
        ws.Cell(15, colC).Value = customer.TaxCode ?? "";

        // Dòng 17: Header bảng
        var headerRow = 17;
        ws.Cell(headerRow, colB).Value = "STT";
        ws.Cell(headerRow, colC).Value = "Tên sản phẩm";
        ws.Cell(headerRow, colD).Value = "Bảo hành";
        ws.Cell(headerRow, colE).Value = "Số lượng";
        ws.Cell(headerRow, colF).Value = "Đơn giá";
        ws.Cell(headerRow, colG).Value = "Thành tiền";

        var headerRange = ws.Range(headerRow, colB, headerRow, colG);
        headerRange.Style.Font.Bold = true;
        ApplyThinBorder(headerRange);
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Dòng 18+: dữ liệu
        var dataStart = headerRow + 1;
        for (var i = 0; i < items.Count; i++)
        {
            var row = dataStart + i;
            var it = items[i];
            ws.Cell(row, colB).Value = it.LineNumber;
            ws.Cell(row, colC).Value = it.ProductName;
            ws.Cell(row, colD).Value = it.Warranty;
            ws.Cell(row, colE).Value = it.Quantity;
            ws.Cell(row, colF).Value = it.UnitPrice;
            ws.Cell(row, colG).Value = it.LineTotal;

            ws.Cell(row, colB).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, colD).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, colE).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, colF).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(row, colG).Style.NumberFormat.Format = MoneyFormat;

            var lineRange = ws.Range(row, colB, row, colG);
            ApplyThinBorder(lineRange);
        }

        var lastDataRow = items.Count == 0 ? headerRow : dataStart + items.Count - 1;

        // Ghi chú (merge B:D) + tổng kết
        var noteTop = lastDataRow + 2;
        var noteRange = ws.Range(noteTop, colB, noteTop + 2, colD);
        noteRange.Merge();
        noteRange.FirstCell().Value = CustomerNoticeText;
        noteRange.Style.Alignment.WrapText = true;
        noteRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        noteRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ApplyThinBorder(noteRange);

        var finStart = noteTop + 3;
        void AddMoneyRow(int row, string labelEf, decimal amount, bool totalRow = false)
        {
            ws.Range(row, colE, row, colF).Merge();
            ws.Cell(row, colE).Value = labelEf;
            ws.Cell(row, colE).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Cell(row, colE).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(row, colG).Value = amount;
            ws.Cell(row, colG).Style.NumberFormat.Format = MoneyFormat;
            if (totalRow)
            {
                ws.Cell(row, colE).Style.Font.Bold = true;
                ws.Cell(row, colE).Style.Font.FontColor = XLColor.Red;
                ws.Cell(row, colG).Style.Font.Bold = true;
                ws.Cell(row, colG).Style.Font.FontColor = XLColor.Red;
            }

            ApplyThinBorder(ws.Range(row, colE, row, colG));
        }

        AddMoneyRow(finStart, "Phí vận chuyển", summary.ShippingFee);
        AddMoneyRow(finStart + 1, "Chi phí khác", summary.OtherCosts);
        AddMoneyRow(finStart + 2, "Được KM giảm tiền mặt", summary.Discount);
        AddMoneyRow(finStart + 3, "Tổng tiền đơn hàng", summary.OrderTotal, totalRow: true);

        // Chữ ký
        var sigTop = finStart + 5;
        var roles = new[] { "Người mua hàng", "Người bán hàng", "Kế toán", "Thủ kho" };
        for (var c = 0; c < roles.Length; c++)
        {
            var col = colB + c;
            ws.Cell(sigTop, col).Value = roles[c];
            ws.Cell(sigTop, col).Style.Font.Bold = true;
            ws.Cell(sigTop, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(sigTop + 1, col).Value = "(Ký, họ tên)";
            ws.Cell(sigTop + 1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(sigTop + 1, col).Style.Font.Italic = true;
        }

        ws.Columns(colB, colG).AdjustToContents();
        if (ws.Column(colC).Width > 55)
            ws.Column(colC).Width = 55;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void ApplyThinBorder(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }
}
