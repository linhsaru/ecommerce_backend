namespace Application.DTOs.Dashboard;

public sealed class GetAdminDashboardStatsRequest
{
    public string PeriodType { get; set; } = "month";

    public string? Date { get; set; }
}

