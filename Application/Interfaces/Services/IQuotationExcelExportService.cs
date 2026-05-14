using Application.Common;
using Application.DTOs.Quotations;

namespace Application.Interfaces.Services;

public interface IQuotationExcelExportService
{
    Task<Result<byte[]>> ExportAsync(PreviewQuotationRequest request, CancellationToken cancellationToken = default);
}
