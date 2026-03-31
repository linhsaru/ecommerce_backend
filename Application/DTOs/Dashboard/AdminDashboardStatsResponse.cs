using System;

namespace Application.DTOs.Dashboard;

public sealed class AdminDashboardStatsResponse
{
    public decimal Revenue { get; set; }
    public int TotalOrders { get; set; }
    public int NewUsers { get; set; }

    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal SuccessRatePercent { get; set; }
    public decimal CancelledRatePercent { get; set; }

    public TopProductDto? TopProduct { get; set; }

    public DateTimeOffset PeriodFrom { get; set; }
    public DateTimeOffset PeriodToExclusive { get; set; }
}

public sealed class TopProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int QuantitySold { get; set; }
}

