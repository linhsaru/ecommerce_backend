using API.Common;
using API.Contracts;
using Application.DTOs.Payments;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("payments/vietqr")]
public class VietQrPaymentsController : BaseApiController
{
    private readonly IPaymentService _paymentService;

    public VietQrPaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("create")]
    [ProducesResponseType(typeof(ApiResponse<CreateVietQrPaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateVietQrPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _paymentService.CreateVietQrPaymentAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<VietQrPaymentStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckStatus([FromQuery] Guid orderId, [FromQuery] string transactionRef, CancellationToken cancellationToken = default)
    {
        var result = await _paymentService.CheckVietQrPaymentStatusAsync(orderId, transactionRef, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("confirm")]
    [HttpPost("webhook")]
    [HttpPost("/webhooks/sepay/vietqr")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<VietQrPaymentStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm([FromBody] VietQrWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _paymentService.ConfirmVietQrPaymentAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }
}
