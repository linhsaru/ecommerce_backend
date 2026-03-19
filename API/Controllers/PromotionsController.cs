using System.Linq;
using API.Common;
using API.Contracts;
using Application.Common;
using Application.DTOs.Promotions;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("promotions")]
public class PromotionsController : BaseApiController
{
    private readonly IPromotionService _promotionService;

    public PromotionsController(IPromotionService promotionService) => _promotionService = promotionService;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<PromotionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _promotionService.GetPagedAsync(page, pageSize, search, status, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Promotions");
        var (items, total) = result.Value!;
        var response = PagedResponse<PromotionDto>.Create(items, page, pageSize, total);
        return Ok(ApiResponse<PagedResponse<PromotionDto>>.Ok(response, traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _promotionService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PromotionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _promotionService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Promotion");
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, ApiResponse<PromotionDto>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PromotionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _promotionService.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _promotionService.DeleteAsync(id, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Promotion");
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
