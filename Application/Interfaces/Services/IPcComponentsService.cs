using Application.Common;
using Application.DTOs.Recommendations;

namespace Application.Interfaces.Services;

public interface IPcComponentsService
{
    Task<Result<PcComponentsCatalogDto>> GetPcComponentsCatalogAsync(CancellationToken cancellationToken = default);
}

