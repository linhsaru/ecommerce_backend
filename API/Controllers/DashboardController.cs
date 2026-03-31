using API.Contracts;
using API.Common;
using Application.Common;
using Application.DTOs.Dashboard;
using Application.Interfaces.Services;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("admin/dashboard")]
public sealed class DashboardController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("stats")]
    [Authorize(Roles = nameof(UserRole.RoleAdmin))]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardStatsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDashboardStats(
        [FromQuery] GetAdminDashboardStatsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _dashboardService.GetAdminDashboardStatsAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }
}

