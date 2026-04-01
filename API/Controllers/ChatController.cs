using API.Common;
using API.Contracts;
using Application.DTOs.Chat;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/chat")]
[AllowAnonymous]
public sealed class ChatController : BaseApiController
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ChatResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _chatService.ChatAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }
}

