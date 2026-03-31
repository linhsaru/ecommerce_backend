using Application.Common;
using Application.DTOs.Dashboard;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;

namespace Application.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IAppDbContext _dbContext;

    public DashboardService(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<AdminDashboardStatsResponse>> GetAdminDashboardStatsAsync(
        GetAdminDashboardStatsRequest request,
        CancellationToken cancellationToken = default)
    {
        var rangeResult = TryParsePeriodRange(request.PeriodType, request.Date);
        if (rangeResult.IsFailure)
            return Result<AdminDashboardStatsResponse>.Fail(rangeResult.Errors.First().Code, rangeResult.Errors.First().Message);

        var (periodFrom, periodToExclusive) = rangeResult.Value!;

        // 1) Revenue: sum completed order totals in period
        // (Using Orders.CompletedAt avoids missing revenue for payment methods
        // that may not update Payment.PaidAt/PaymentStatus.)
        var revenue = await _dbContext.Orders
            .AsNoTracking()
            .Where(o =>
                o.Status == OrderStatus.completed
                && o.CompletedAt != null
                && o.CompletedAt >= periodFrom
                && o.CompletedAt < periodToExclusive)
            .SumAsync(o => o.TotalAmount, cancellationToken);

        // 2) Total orders: created orders in period
        var totalOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= periodFrom && o.CreatedAt < periodToExclusive)
            .CountAsync(cancellationToken);

        // 3) New users: created users in period (exclude soft-deleted)
        var newUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.DeletedAt == null && u.CreatedAt >= periodFrom && u.CreatedAt < periodToExclusive)
            .CountAsync(cancellationToken);

        // 4) Success/cancel counts based on completed/cancelled timestamps in period
        var completedOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o =>
                o.Status == OrderStatus.completed
                && o.CompletedAt != null
                && o.CompletedAt >= periodFrom
                && o.CompletedAt < periodToExclusive)
            .CountAsync(cancellationToken);

        var cancelledOrders = await _dbContext.Orders
            .AsNoTracking()
            .Where(o =>
                o.Status == OrderStatus.cancelled
                && o.CancelledAt != null
                && o.CancelledAt >= periodFrom
                && o.CancelledAt < periodToExclusive)
            .CountAsync(cancellationToken);

        // Rates vs total orders in period (so pending orders do not distort cancellation rate)
        decimal successRatePercent = totalOrders == 0
            ? 0
            : (decimal)completedOrders / totalOrders * 100m;

        decimal cancelledRatePercent = totalOrders == 0
            ? 0
            : (decimal)cancelledOrders / totalOrders * 100m;

        // 5) Top sold product (by quantity) from completed orders in period
        var topProduct = await (
            from oi in _dbContext.OrderItems.AsNoTracking()
            join o in _dbContext.Orders.AsNoTracking() on oi.OrderId equals o.Id
            where o.Status == OrderStatus.completed
                && o.CompletedAt != null
                && o.CompletedAt >= periodFrom
                && o.CompletedAt < periodToExclusive
            group oi by new { oi.ProductId, ProductName = oi.Name } into g
            orderby g.Sum(x => x.Quantity) descending
            select new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                QuantitySold = g.Sum(x => x.Quantity)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Result<AdminDashboardStatsResponse>.Ok(new AdminDashboardStatsResponse
        {
            Revenue = revenue,
            TotalOrders = totalOrders,
            NewUsers = newUsers,
            CompletedOrders = completedOrders,
            CancelledOrders = cancelledOrders,
            SuccessRatePercent = successRatePercent,
            CancelledRatePercent = cancelledRatePercent,
            TopProduct = topProduct,
            PeriodFrom = periodFrom,
            PeriodToExclusive = periodToExclusive
        });
    }

    private static Result<(DateTimeOffset from, DateTimeOffset toExclusive)> TryParsePeriodRange(
        string? periodType,
        string? date)
    {
        var normalized = (periodType ?? "month").Trim();
        if (!Enum.TryParse<PeriodType>(normalized, ignoreCase: true, out var parsedPeriod))
            return Result<(DateTimeOffset from, DateTimeOffset toExclusive)>.Fail(
                "VALIDATION_ERROR",
                "periodType không hợp lệ. Chỉ hỗ trợ: day, month, year.");

        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(date))
        {
            return OkByPeriod(parsedPeriod, now);
        }

        try
        {
            return parsedPeriod switch
            {
                PeriodType.day => ParseByExact(date!, "yyyy-MM-dd", d => OkByPeriod(parsedPeriod, d)),
                PeriodType.month => ParseByExact(date!, "yyyy-MM", d => OkByPeriod(parsedPeriod, d)),
                PeriodType.year => ParseByExact(date!, "yyyy", d => OkByPeriod(parsedPeriod, d)),
                _ => Result<(DateTimeOffset from, DateTimeOffset toExclusive)>.Fail("VALIDATION_ERROR", "periodType không hợp lệ.")
            };
        }
        catch (Exception)
        {
            return Result<(DateTimeOffset from, DateTimeOffset toExclusive)>.Fail(
                "VALIDATION_ERROR",
                "date không đúng định dạng yêu cầu cho periodType.");
        }

        static Result<(DateTimeOffset from, DateTimeOffset toExclusive)> OkByPeriod(PeriodType type, DateTimeOffset baseDate)
        {
            var from = type switch
            {
                PeriodType.day => new DateTimeOffset(baseDate.Year, baseDate.Month, baseDate.Day, 0, 0, 0, TimeSpan.Zero),
                PeriodType.month => new DateTimeOffset(baseDate.Year, baseDate.Month, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodType.year => new DateTimeOffset(baseDate.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                _ => new DateTimeOffset(baseDate.Year, baseDate.Month, baseDate.Day, 0, 0, 0, TimeSpan.Zero)
            };
            var toExclusive = type switch
            {
                PeriodType.day => from.AddDays(1),
                PeriodType.month => from.AddMonths(1),
                PeriodType.year => from.AddYears(1),
                _ => from.AddDays(1)
            };
            return Result<(DateTimeOffset from, DateTimeOffset toExclusive)>.Ok((from, toExclusive));
        }

        static Result<(DateTimeOffset from, DateTimeOffset toExclusive)> ParseByExact(
            string dateStr,
            string format,
            Func<DateTimeOffset, Result<(DateTimeOffset from, DateTimeOffset toExclusive)>> mapper)
        {
            if (!DateTime.TryParseExact(
                    dateStr,
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dt))
            {
                return Result<(DateTimeOffset from, DateTimeOffset toExclusive)>.Fail(
                    "VALIDATION_ERROR",
                    $"Định dạng date không hợp lệ. Dùng format {format}.");
            }

            // Force to UTC midnight/month/year boundary.
            var baseDate = new DateTimeOffset(dt.Year, dt.Month, dt.Day, 0, 0, 0, TimeSpan.Zero);
            return mapper(baseDate);
        }
    }

    private enum PeriodType
    {
        day,
        month,
        year
    }
}

