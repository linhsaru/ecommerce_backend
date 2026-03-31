using Application.Common;
using Application.DTOs.Promotions;

namespace Application.Interfaces.Services;

public interface IPromotionService
{
    Task<Result<(List<PromotionDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, string? status, CancellationToken cancellationToken = default);
    Task<Result<PromotionDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PromotionDto>> CreateAsync(CreatePromotionRequest request, CancellationToken cancellationToken = default);
    Task<Result<PromotionDto>> UpdateAsync(Guid id, UpdatePromotionRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
