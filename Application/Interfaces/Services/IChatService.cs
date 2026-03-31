using Application.Common;
using Application.DTOs.Chat;

namespace Application.Interfaces.Services;

public interface IChatService
{
    Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

