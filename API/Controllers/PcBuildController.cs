using API.Common;
using API.Contracts;
using Application.DTOs.Recommendations;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/pc-build")]
[AllowAnonymous]
public sealed class PcBuildController : BaseApiController
{
    private readonly IPcComponentsService _pcComponentsService;
    private readonly IPcBuildGeminiSuggestionService _pcBuildGeminiSuggestionService;

    public PcBuildController(
        IPcComponentsService pcComponentsService,
        IPcBuildGeminiSuggestionService pcBuildGeminiSuggestionService)
    {
        _pcComponentsService = pcComponentsService;
        _pcBuildGeminiSuggestionService = pcBuildGeminiSuggestionService;
    }

    [HttpGet("components")]
    [ProducesResponseType(typeof(ApiResponse<PcComponentsCatalogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetComponentsCatalog(CancellationToken cancellationToken = default)
    {
        var result = await _pcComponentsService.GetPcComponentsCatalogAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("gemini-suggestions")]
    [ProducesResponseType(typeof(ApiResponse<PcBuildGeminiSuggestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GeminiSuggestions([FromBody] PcBuildGeminiSuggestRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _pcBuildGeminiSuggestionService.SuggestAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }
}

