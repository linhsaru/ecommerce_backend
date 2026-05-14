using Application.Common;
using Application.DTOs.Quotations;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Interfaces.Repositories;

namespace Application.Services;

//Tạo dữ liệu báo giá từ danh sách sản phẩm trong DB
public sealed class QuotationService : IQuotationService
{
    private const string DefaultWarranty = "36 Tháng";
    private const string DefaultNotes =
        "Giá linh kiện có thể thay đổi theo thời điểm nhập hàng và chương trình khuyến mãi. Vui lòng xác nhận lại giá và tồn kho trước khi đặt cọc.";

    private static readonly CompanyInfoDto DefaultCompany = new()
    {
        CompanyName = "Công ty TNHH Máy tính Linh Hiệp",
        AddressLine1 = "Xóm Châu Nhân 2, xã Lam Thành, tỉnh Nghệ An",
        AddressLine2 = "Khối 8, Phường Trường Vinh, tỉnh Nghệ An",
        Hotline = "098 137 2813",
        Email = "lhcomputer.work@gmail.com",
        Website = "www.linhiepcomputer.vn"
    };

    private readonly IProductRepository _productRepo;

    public QuotationService(IProductRepository productRepo)
    {
        _productRepo = productRepo;
    }

    public async Task<Result<QuotationResponseDto>> PreviewAsync(PreviewQuotationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items == null || request.Items.Count == 0)
            return Result<QuotationResponseDto>.Fail("VALIDATION_ERROR", "Danh sách sản phẩm không được để trống.");

        var shipping = Math.Max(0, request.ShippingFee);
        var other = Math.Max(0, request.OtherCosts);
        var discount = Math.Max(0, request.Discount);

        var items = new List<QuotationItemDto>();
        var lineNo = 1;

        foreach (var line in request.Items)
        {
            if (line.ProductId == Guid.Empty)
                return Result<QuotationResponseDto>.Fail("VALIDATION_ERROR", "ProductId không hợp lệ.");

            var qty = Math.Max(1, line.Quantity);
            var product = await _productRepo.GetByIdAsync(line.ProductId, cancellationToken);
            if (product == null)
                return Result<QuotationResponseDto>.Fail("NOT_FOUND", $"Không tìm thấy sản phẩm: {line.ProductId}.");

            var variant = ResolveVariant(product, line.VariantId);
            if (variant == null)
                return Result<QuotationResponseDto>.Fail("VALIDATION_ERROR", $"Sản phẩm \"{product.Name}\" không có biến thể đang bán.");

            var unit = Math.Round(variant.Price, 2, MidpointRounding.AwayFromZero);
            var lineTotal = Math.Round(unit * qty, 2, MidpointRounding.AwayFromZero);

            items.Add(new QuotationItemDto
            {
                LineNumber = lineNo++,
                ProductName = product.Name,
                Warranty = ExtractWarranty(variant),
                Quantity = qty,
                UnitPrice = unit,
                LineTotal = lineTotal
            });
        }

        var subTotal = Math.Round(items.Sum(i => i.LineTotal), 2, MidpointRounding.AwayFromZero);
        var payableBeforeDiscount = subTotal + shipping + other;
        if (discount > payableBeforeDiscount)
            return Result<QuotationResponseDto>.Fail("VALIDATION_ERROR", "Giảm giá không được vượt quá tổng tiền (tạm tính + phí).");

        var orderTotal = Math.Round(payableBeforeDiscount - discount, 2, MidpointRounding.AwayFromZero);

        var quotationDate = request.QuotationDate ?? TodayVietnam();
        var customer = new CustomerInfoDto
        {
            CustomerName = request.Customer?.CustomerName?.Trim() ?? "",
            Address = request.Customer?.Address?.Trim() ?? "",
            TaxCode = string.IsNullOrWhiteSpace(request.Customer?.TaxCode) ? null : request.Customer!.TaxCode!.Trim(),
            QuotationDate = quotationDate
        };

        var response = new QuotationResponseDto
        {
            Company = DefaultCompany,
            Customer = customer,
            Items = items,
            Summary = new QuotationSummaryDto
            {
                SubTotal = subTotal,
                ShippingFee = shipping,
                OtherCosts = other,
                Discount = discount,
                OrderTotal = orderTotal
            },
            Notes = DefaultNotes
        };

        return Result<QuotationResponseDto>.Ok(response);
    }

    private static ProductVariant? ResolveVariant(Product product, Guid? variantId)
    {
        var active = (product.ProductVariants ?? Enumerable.Empty<ProductVariant>())
            .Where(v => v.DeletedAt == null && v.Status == 1)
            .ToList();

        if (active.Count == 0)
            return null;

        if (variantId.HasValue && variantId.Value != Guid.Empty)
        {
            var v = active.FirstOrDefault(x => x.Id == variantId.Value);
            return v;
        }

        return active.OrderBy(v => v.Price).ThenBy(v => v.Sku).First();
    }

    private static string ExtractWarranty(ProductVariant variant)
    {
        foreach (var s in variant.ProductVariantSpecifications ?? Enumerable.Empty<ProductVariantSpecification>())
        {
            var name = s.SpecificationType?.Name ?? "";
            if (name.Contains("bảo hành", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("bao hanh", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("warranty", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(s.Value))
                    return s.Value.Trim();
            }
        }

        return DefaultWarranty;
    }

    private static DateOnly TodayVietnam()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                return DateOnly.FromDateTime(local);
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
    }
}
