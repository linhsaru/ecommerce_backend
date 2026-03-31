using Application.Common;
using Application.DTOs.Chat;
using Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Application.Services;

public sealed class ChatService : IChatService
{
    private const string OpenAiChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const string DefaultModel = "gpt-4o-mini";

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public ChatService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
            return Result<ChatResponse>.Fail("VALIDATION_ERROR", "Message is required.");

        var apiKey = _configuration["BotChat:KeyBot"]?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return Result<ChatResponse>.Fail("CONFIG_ERROR", "OpenAI API key is missing (BotChat:KeyBot).");

        var client = _httpClientFactory.CreateClient("OpenAI");

        var requestedModel = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model!.Trim();
        var defaultModelFromConfig = _configuration["BotChat:DefaultModel"]?.Trim();

        var modelsToTry = requestedModel is not null
            ? new[] { requestedModel }
            : new[]
            {
                string.IsNullOrWhiteSpace(defaultModelFromConfig) ? DefaultModel : defaultModelFromConfig,
                "gpt-4o-mini",
                "gpt-4o",
                "gpt-4.1-mini",
                "gpt-3.5-turbo"
            };

        var distinctModels = modelsToTry
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? lastRaw = null;
        string? lastModelUsed = null;

        foreach (var model in distinctModels)
        {
            lastModelUsed = model;
            var payload = new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = request.Message }
                },
                temperature = 0.7,
                max_tokens = 512
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAiChatCompletionsUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = JsonContent.Create(payload);

            using var httpResponse = await client.SendAsync(httpRequest, cancellationToken);
            var raw = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            lastRaw = raw;

            if (!httpResponse.IsSuccessStatusCode)
            {
                if (raw.Contains("invalid model", StringComparison.OrdinalIgnoreCase) ||
                    raw.Contains("invalid_model", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Result<ChatResponse>.Fail(
                    "OPENAI_ERROR",
                    "OpenAI request failed.",
                    $"Model={model}. Raw={raw}");
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(
                    raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var reply = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(reply))
                    return Result<ChatResponse>.Fail("OPENAI_ERROR", "OpenAI returned an empty reply.", raw);

                return Result<ChatResponse>.Ok(new ChatResponse
                {
                    Reply = reply.Trim(),
                    ModelUsed = model
                });
            }
            catch (JsonException)
            {
                return Result<ChatResponse>.Fail("OPENAI_ERROR", "Failed to parse OpenAI response.", raw);
            }
        }

        return Result<ChatResponse>.Fail(
            "OPENAI_ERROR",
            "OpenAI request failed (invalid/unsupported model).",
            lastRaw ?? $"Model tried={lastModelUsed}");
    }

    private sealed class ChatCompletionResponse
    {
        public List<ChatChoice> Choices { get; set; } = new();
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        public string? Content { get; set; }
    }
}

