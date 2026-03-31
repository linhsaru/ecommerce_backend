using Application.Common;
using Application.DTOs.Coupons;

namespace Application.Interfaces.Services;

public interface ICouponService
{
    Task<Result<(List<CouponDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default);
    Task<Result<CouponDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<CouponDto?>> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Result<CouponDto>> CreateAsync(CreateCouponRequest request, CancellationToken cancellationToken = default);
    Task<Result<CouponDto>> UpdateAsync(Guid id, UpdateCouponRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
