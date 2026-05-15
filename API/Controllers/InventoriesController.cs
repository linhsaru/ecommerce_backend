using System.Linq;
using API.Common;
using API.Contracts;
using Application.Common;
using Application.DTOs.Inventories;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("inventories")]
public class InventoriesController : BaseApiController
{
    private readonly IInventoryService _inventoryService;

    public InventoriesController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<InventoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? variantId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _inventoryService.GetPagedAsync(page, pageSize, warehouseId, variantId, search, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Inventories");
        var (items, total) = result.Value!;
        var response = PagedResponse<InventoryDto>.Create(items, page, pageSize, total);
        return Ok(ApiResponse<PagedResponse<InventoryDto>>.Ok(response, traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("warehouse-summaries")]
    [ProducesResponseType(typeof(ApiResponse<List<WarehouseInventorySummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWarehouseSummaries(CancellationToken cancellationToken = default)
    {
        var result = await _inventoryService.GetWarehouseSummariesAsync(cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Inventories");
        return Ok(ApiResponse<List<WarehouseInventorySummaryDto>>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("warehouse/{warehouseId:guid}/variant/{variantId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InventoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByKey(Guid warehouseId, Guid variantId, CancellationToken cancellationToken = default)
    {
        var result = await _inventoryService.GetByKeyAsync(warehouseId, variantId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InventoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrUpdate([FromBody] CreateOrUpdateInventoryRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _inventoryService.CreateOrUpdateAsync(request, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Inventory");
        return Ok(ApiResponse<InventoryDto>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
    }

    [HttpDelete("warehouse/{warehouseId:guid}/variant/{variantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid warehouseId, Guid variantId, CancellationToken cancellationToken = default)
    {
        var result = await _inventoryService.DeleteAsync(warehouseId, variantId, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Inventory");
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
