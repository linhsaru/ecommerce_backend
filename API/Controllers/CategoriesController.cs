using System.Linq;
using API.Common;
using API.Contracts;
using Application.DTOs.Categories;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// REST API cho danh muc: GET phan trang, GET theo id/slug, POST tao, PUT cap nhat, DELETE xoa mem.
/// </summary>
[ApiController]
[Route("categories")]
public class CategoriesController : BaseApiController
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// GET categories?page=1&pageSize=10&search=...&parentId=...
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<CategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] long? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetPagedAsync(page, pageSize, search, parentId, cancellationToken);
        if (result.IsFailure)
        {
            var errors = result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList();
            return StatusCode(400, ApiResponse<object>.Fail("Request failed", errors, HttpContext.TraceIdentifier));
        }
        var (items, total) = result.Value!;
        var response = PagedResponse<CategoryDto>.Create(items, page, pageSize, total);
        return Ok(ApiResponse<PagedResponse<CategoryDto>>.Ok(response, traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// GET categories/{id}
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetByIdAsync(id, cancellationToken);
        if (result.IsSuccess && result.Value != null)
            return Ok(ApiResponse<CategoryDto>.Ok(result.Value, traceId: HttpContext.TraceIdentifier));
        if (result.Errors.Any(e => e.Code == "NOT_FOUND"))
            return NotFound(ApiResponse<CategoryDto>.Fail("Not found", result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList(), HttpContext.TraceIdentifier));
        return BadRequest(ApiResponse<CategoryDto>.Fail("Request failed", result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList(), HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// GET categories/slug/{slug}
    /// </summary>
    [HttpGet("slug/{slug}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.GetBySlugAsync(slug, cancellationToken);
        if (result.IsSuccess && result.Value != null)
            return Ok(ApiResponse<CategoryDto>.Ok(result.Value, traceId: HttpContext.TraceIdentifier));
        if (result.Errors.Any(e => e.Code == "NOT_FOUND"))
            return NotFound(ApiResponse<CategoryDto>.Fail("Not found", result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList(), HttpContext.TraceIdentifier));
        return BadRequest(ApiResponse<CategoryDto>.Fail("Request failed", result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList(), HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// POST categories
    /// </summary>
    [HttpPost]
    [Route("category")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            var errors = result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList();
            return BadRequest(ApiResponse<CategoryDto>.Fail(result.Errors.First().Message, errors, HttpContext.TraceIdentifier));
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, ApiResponse<CategoryDto>.Ok(result.Value, traceId: HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// PUT categories/{id}
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.UpdateAsync(id, request, cancellationToken);
        if (result.IsSuccess)
            return Ok(ApiResponse<CategoryDto>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
        if (result.Errors.Any(e => e.Code == "NOT_FOUND"))
            return NotFound(ApiResponse<CategoryDto>.Fail("Not found", result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList(), HttpContext.TraceIdentifier));
        return BadRequest(ApiResponse<CategoryDto>.Fail("Request failed", result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList(), HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// DELETE categories/{id}
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _categoryService.DeleteAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Errors.Any(e => e.Code == "NOT_FOUND"))
                return NotFound(ApiResponse<object>.Fail("Not found", result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList(), HttpContext.TraceIdentifier));
            return BadRequest(ApiResponse<object>.Fail("Request failed", result.Errors.Select(e => new ApiError(e.Code, e.Message, Detail: e.Details)).ToList(), HttpContext.TraceIdentifier));
        }
        return NoContent();
    }
}
