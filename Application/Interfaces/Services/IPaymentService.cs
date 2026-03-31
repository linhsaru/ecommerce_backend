using Application.Common;
using Application.DTOs.Payments;

namespace Application.Interfaces.Services;

public interface IPaymentService
{
    Task<Result<CreateVnPayPaymentResponse>> CreateVnPayPaymentUrlAsync(
        CreateVnPayPaymentRequest request,
        string clientIp,
        CancellationToken cancellationToken = default);

    Task<Result<VnPayCallbackResponse>> HandleVnPayCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);
}
