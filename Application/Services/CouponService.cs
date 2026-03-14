using Application.Common;
using Application.DTOs.Coupons;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class CouponService : ICouponService
{
    private readonly ICouponRepository _repo;

    public CouponService(ICouponRepository repo) => _repo = repo;

    public async Task<Result<(List<CouponDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default)
    {
        var query = _repo.GetQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Code.Contains(search) || (c.Name != null && c.Name.Contains(search)));
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var total = await query.LongCountAsync(cancellationToken);
        var skip = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((int)skip)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(c => new CouponDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                MinOrderValue = c.MinOrderValue,
                MaxDiscount = c.MaxDiscount,
                UsageLimit = c.UsageLimit,
                UsageCount = c.UsageCount,
                StartAt = c.StartAt,
                EndAt = c.EndAt,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<(List<CouponDto> Items, long Total)>.Ok((items, total));
    }

    public async Task<Result<CouponDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var c = await _repo.GetByIdAsync(id, cancellationToken);
        if (c == null)
            return Result<CouponDto?>.Fail("NOT_FOUND", "Coupon not found.");
        return Result<CouponDto?>.Ok(Map(c));
    }

    public async Task<Result<CouponDto?>> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var c = await _repo.GetByCodeAsync(code, cancellationToken);
        if (c == null)
            return Result<CouponDto?>.Fail("NOT_FOUND", "Coupon not found.");
        return Result<CouponDto?>.Ok(Map(c));
    }

    public async Task<Result<CouponDto>> CreateAsync(CreateCouponRequest request, CancellationToken cancellationToken = default)
    {
        if (await _repo.ExistsByCodeAsync(request.Code, null, cancellationToken))
            return Result<CouponDto>.Fail("VALIDATION_ERROR", "Coupon code already exists.");

        var now = DateTimeOffset.UtcNow;
        var coupon = new Coupon
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MinOrderValue = request.MinOrderValue,
            MaxDiscount = request.MaxDiscount,
            UsageLimit = request.UsageLimit,
            UsageCount = 0,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Status = request.Status,
            CreatedAt = now,
            UpdatedAt = now
        };
        _repo.Add(coupon);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<CouponDto>.Ok(Map(coupon));
    }

    public async Task<Result<CouponDto>> UpdateAsync(Guid id, UpdateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var c = await _repo.GetByIdAsync(id, cancellationToken);
        if (c == null)
            return Result<CouponDto>.Fail("NOT_FOUND", "Coupon not found.");

        if (request.Code != null)
        {
            if (await _repo.ExistsByCodeAsync(request.Code, id, cancellationToken))
                return Result<CouponDto>.Fail("VALIDATION_ERROR", "Coupon code already exists.");
            c.Code = request.Code;
        }
        if (request.Name != null) c.Name = request.Name;
        if (request.DiscountType != null) c.DiscountType = request.DiscountType;
        if (request.DiscountValue.HasValue) c.DiscountValue = request.DiscountValue.Value;
        if (request.MinOrderValue.HasValue) c.MinOrderValue = request.MinOrderValue.Value;
        if (request.MaxDiscount.HasValue) c.MaxDiscount = request.MaxDiscount;
        if (request.UsageLimit.HasValue) c.UsageLimit = request.UsageLimit;
        if (request.StartAt.HasValue) c.StartAt = request.StartAt;
        if (request.EndAt.HasValue) c.EndAt = request.EndAt;
        if (request.Status.HasValue) c.Status = request.Status.Value;
        c.UpdatedAt = DateTimeOffset.UtcNow;

        _repo.Update(c);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<CouponDto>.Ok(Map(c));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var c = await _repo.GetByIdAsync(id, cancellationToken);
        if (c == null)
            return Result.Fail("NOT_FOUND", "Coupon not found.");
        _repo.Remove(c);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static CouponDto Map(Coupon c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        DiscountType = c.DiscountType,
        DiscountValue = c.DiscountValue,
        MinOrderValue = c.MinOrderValue,
        MaxDiscount = c.MaxDiscount,
        UsageLimit = c.UsageLimit,
        UsageCount = c.UsageCount,
        StartAt = c.StartAt,
        EndAt = c.EndAt,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
