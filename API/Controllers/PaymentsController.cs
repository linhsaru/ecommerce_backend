using API.Common;
using API.Contracts;
using Application.DTOs.Payments;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("payments/vnpay")]
public class PaymentsController : BaseApiController
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("create-url")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CreateVnPayPaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePaymentUrl(
        [FromBody] CreateVnPayPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var result = await _paymentService.CreateVnPayPaymentUrlAsync(request, clientIp, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("return")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<VnPayCallbackResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Return(CancellationToken cancellationToken = default)
    {
        var query = HttpContext.Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var result = await _paymentService.HandleVnPayCallbackAsync(query, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("ipn")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Ipn(CancellationToken cancellationToken = default)
    {
        var query = HttpContext.Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var result = await _paymentService.HandleVnPayCallbackAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            var code = result.Errors.Any(e => e.Code == "NOT_FOUND") ? "01" : "97";
            return Ok(new { RspCode = code, Message = result.Errors.FirstOrDefault()?.Message ?? "Invalid data" });
        }

        return Ok(new { RspCode = "00", Message = "Confirm Success" });
    }
}
