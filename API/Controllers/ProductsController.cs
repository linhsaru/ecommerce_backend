using System.Linq;
using API.Common;
using API.Contracts;
using Application.Common;
using Application.DTOs.Products;
using Application.Interfaces.Services;
using Domain.Enums;
using Domain.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// REST API cho san pham: GET phan trang, GET theo id/slug, POST tao, PUT cap nhat, DELETE xoa mem.
/// </summary>
[Authorize]
[ApiController]
[Route("products")]
public class ProductsController : BaseApiController
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ProductDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? status = null,
        [FromQuery] List<Guid>? categoryId = null,
        [FromQuery] string? categorySlug = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _productService.GetPagedAsync(page, pageSize, search, status, categoryId, categorySlug, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Products");

        var (items, total) = result.Value!;
        var response = PagedResponse<ProductDto>.Create(items, page, pageSize, total);
        return Ok(ApiResponse<PagedResponse<ProductDto>>.Ok(response, traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("by-category/{categorySlug}")]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ProductDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetByCategorySlug(
        string categorySlug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _productService.GetPagedAsync(page, pageSize, search, status, categoryId: null, categorySlug, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Products");

        var (items, total) = result.Value!;
        var response = PagedResponse<ProductDto>.Create(items, page, pageSize, total);
        return Ok(ApiResponse<PagedResponse<ProductDto>>.Ok(response, traceId: HttpContext.TraceIdentifier));
    }

    
    [HttpGet("{id:guid}/variants")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductVariantDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetVariantsByProductId(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _productService.GetVariantsByProductIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /api/products/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _productService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /api/products/slug/{slug}
    /// </summary>
    [HttpGet("slug/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken = default)
    {
        var result = await _productService.GetBySlugAsync(slug, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// POST /api/products
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Roles = nameof(UserRole.RoleAdmin))]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _productService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Product");
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, ApiResponse<ProductDto>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// PUT /api/products/{id}
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = nameof(UserRole.RoleAdmin))]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _productService.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// DELETE /api/products/{id}
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Roles = nameof(UserRole.RoleAdmin))]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _productService.DeleteAsync(id, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Product");
        return NoContent();
    }

    private IActionResult ResultToStatus(Application.Common.Result result, string resourceName)
    {
        var traceId = HttpContext.TraceIdentifier;
        var hasNotFound = result.Errors.Any(e => e.Code == "NOT_FOUND");
        var authorization = result.Errors.Any(e => e.Code == "FORBIDDEN");
        var status = hasNotFound ? 404 : 400;
        var errors = result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList();
        return StatusCode(status, ApiResponse<object>.Fail(result.Errors.FirstOrDefault()?.Message ?? "Request failed", errors, traceId));
    }
}
