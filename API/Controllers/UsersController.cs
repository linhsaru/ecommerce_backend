using System.Linq;
using API.Common;
using API.Contracts;
using Application.Common;
using Application.DTOs.Users;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("users")]
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] int? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _userService.GetPagedAsync(page, pageSize, search, status, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "Users");
        var (items, total) = result.Value!;
        var response = PagedResponse<UserDto>.Create(items, page, pageSize, total);
        return Ok(ApiResponse<PagedResponse<UserDto>>.Ok(response, traceId: HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _userService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "User");
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, ApiResponse<UserDto>.Ok(result.Value!, traceId: HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _userService.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _userService.DeleteAsync(id, cancellationToken);
        if (result.IsFailure)
            return ResultToStatus(result, "User");
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
