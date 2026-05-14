using Application.Common;
using Application.DTOs.Quotations;

namespace Application.Interfaces.Services;

public interface IQuotationService
{
    Task<Result<QuotationResponseDto>> PreviewAsync(PreviewQuotationRequest request, CancellationToken cancellationToken = default);
}
