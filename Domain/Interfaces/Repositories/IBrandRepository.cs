using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IBrandRepository
{
    Task<IReadOnlyList<(Brand Brand, int ProductCount)>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(Brand? Brand, int ProductCount)> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
