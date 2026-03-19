using Application.Common;
using Application.DTOs.Promotions;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class PromotionService : IPromotionService
{
    private readonly IPromotionRepository _repo;

    public PromotionService(IPromotionRepository repo) => _repo = repo;

    public async Task<Result<(List<PromotionDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, string? status, CancellationToken cancellationToken = default)
    {
        var query = _repo.GetQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Title.Contains(search) || (p.Description != null && p.Description.Contains(search)));
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        var total = await query.LongCountAsync(cancellationToken);
        var skip = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((int)skip)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(p => new PromotionDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                BannerImage = p.BannerImage,
                DiscountType = p.DiscountType,
                DiscountValue = p.DiscountValue,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<(List<PromotionDto> Items, long Total)>.Ok((items, total));
    }

    public async Task<Result<PromotionDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _repo.GetByIdAsync(id, cancellationToken);
        if (p == null)
            return Result<PromotionDto?>.Fail("NOT_FOUND", "Promotion not found.");
        return Result<PromotionDto?>.Ok(Map(p));
    }

    public async Task<Result<PromotionDto>> CreateAsync(CreatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var promotion = new Promotion
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            BannerImage = request.BannerImage,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status ?? "active",
            CreatedAt = now,
            UpdatedAt = now
        };
        _repo.Add(promotion);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<PromotionDto>.Ok(Map(promotion));
    }

    public async Task<Result<PromotionDto>> UpdateAsync(Guid id, UpdatePromotionRequest request, CancellationToken cancellationToken = default)
    {
        var p = await _repo.GetByIdAsync(id, cancellationToken);
        if (p == null)
            return Result<PromotionDto>.Fail("NOT_FOUND", "Promotion not found.");

        if (request.Title != null) p.Title = request.Title;
        if (request.Description != null) p.Description = request.Description;
        if (request.BannerImage != null) p.BannerImage = request.BannerImage;
        if (request.DiscountType != null) p.DiscountType = request.DiscountType;
        if (request.DiscountValue.HasValue) p.DiscountValue = request.DiscountValue.Value;
        if (request.StartDate.HasValue) p.StartDate = request.StartDate;
        if (request.EndDate.HasValue) p.EndDate = request.EndDate;
        if (request.Status != null) p.Status = request.Status;
        p.UpdatedAt = DateTimeOffset.UtcNow;

        _repo.Update(p);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result<PromotionDto>.Ok(Map(p));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _repo.GetByIdAsync(id, cancellationToken);
        if (p == null)
            return Result.Fail("NOT_FOUND", "Promotion not found.");
        _repo.Remove(p);
        await _repo.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static PromotionDto Map(Promotion p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Description = p.Description,
        BannerImage = p.BannerImage,
        DiscountType = p.DiscountType,
        DiscountValue = p.DiscountValue,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        Status = p.Status,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
