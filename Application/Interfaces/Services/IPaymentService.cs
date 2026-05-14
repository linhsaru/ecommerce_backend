using Application.Common;
using Application.DTOs.Payments;

namespace Application.Interfaces.Services;

public interface IPaymentService
{
    Task<Result<CreateVietQrPaymentResponse>> CreateVietQrPaymentAsync(
        CreateVietQrPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<VietQrPaymentStatusResponse>> CheckVietQrPaymentStatusAsync(
        Guid orderId,
        string transactionRef,
        CancellationToken cancellationToken = default);

    Task<Result<VietQrPaymentStatusResponse>> ConfirmVietQrPaymentAsync(
        VietQrWebhookRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CreateVnPayPaymentResponse>> CreateVnPayPaymentUrlAsync(
        CreateVnPayPaymentRequest request,
        string clientIp,
        CancellationToken cancellationToken = default);

    Task<Result<VnPayCallbackResponse>> HandleVnPayCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);
}
