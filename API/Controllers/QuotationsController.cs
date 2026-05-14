using System.Linq;
using API.Common;
using API.Contracts;
using Application.DTOs.Quotations;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/quotations")]
[AllowAnonymous]
public sealed class QuotationsController : BaseApiController
{
    private readonly IQuotationService _quotationService;
    private readonly IQuotationExcelExportService _quotationExcelExportService;

    public QuotationsController(
        IQuotationService quotationService,
        IQuotationExcelExportService quotationExcelExportService)
    {
        _quotationService = quotationService;
        _quotationExcelExportService = quotationExcelExportService;
    }

    //POST /api/quotations/preview — tính báo giá từ danh sách ProductId
    [HttpPost("preview")]
    [ProducesResponseType(typeof(ApiResponse<QuotationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview([FromBody] PreviewQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _quotationService.PreviewAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }

    //POST /api/quotations/export-excel — xuất file .xlsx báo giá.
    [HttpPost("export-excel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportExcel([FromBody] PreviewQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _quotationExcelExportService.ExportAsync(request, cancellationToken);
        if (result.IsFailure || result.Value is null)
        {
            var traceId = HttpContext.TraceIdentifier;
            var hasNotFound = result.Errors.Any(e => e.Code == "NOT_FOUND");
            var status = hasNotFound ? 404 : 400;
            var errors = result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList();
            return StatusCode(status, ApiResponse<object>.Fail(result.Errors.FirstOrDefault()?.Message ?? "Export failed", errors, traceId));
        }

        return File(
            result.Value,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Bao_Gia_PC.xlsx");
    }
}
