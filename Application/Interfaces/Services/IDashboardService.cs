using Application.Common;
using Application.DTOs.Dashboard;

namespace Application.Interfaces.Services;

public interface IDashboardService
{
    Task<Result<AdminDashboardStatsResponse>> GetAdminDashboardStatsAsync(
        GetAdminDashboardStatsRequest request,
        CancellationToken cancellationToken = default);
}

