using Application.Common;
using Application.DTOs.Recommendations;

namespace Application.Interfaces.Services;

public interface IPcBuildGeminiSuggestionService
{
    Task<Result<PcBuildGeminiSuggestResponse>> SuggestAsync(PcBuildGeminiSuggestRequest request, CancellationToken cancellationToken = default);
}

