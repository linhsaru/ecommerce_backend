using System.Linq;
using API.Common;
using API.Contracts;
using Application.Common;
using Application.DTOs.Coupons;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("coupons")]
public class CouponsController : BaseApiController
{
    private readonly ICouponService _couponService;

    public CouponsController(ICouponService couponService) => _couponService = couponService;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<CouponDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _couponService.GetPagedAsync(page, pageSize, search, status, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Coupons");
        var (items, total) = result.Value!;
        var response = PagedResponse<CouponDto>.Create(items, page, pageSize, total);
        return Ok(ApiResponse<PagedResponse<CouponDto>>.Ok(response, traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _couponService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("code/{code}")]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken = default)
    {
        var result = await _couponService.GetByCodeAsync(code, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _couponService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Coupon");
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, ApiResponse<CouponDto>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CouponDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _couponService.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _couponService.DeleteAsync(id, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Coupon");
        return NoContent();
    }

    private IActionResult ResultToStatus(Result result, string resourceName)
    {
        var traceId = HttpContext.TraceIdentifier;
        var hasNotFound = result.Errors.Any(e => e.Code == "NOT_FOUND");
        var status = hasNotFound ? 404 : 400;
        var errors = result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList();
        return StatusCode(status, ApiResponse<object>.Fail(result.Errors.FirstOrDefault()?.Message ?? "Request failed", errors, traceId));
    }
}
