using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.DTOs.Chat;

public sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("functionCall")]
    public GeminiFunctionCall? FunctionCall { get; set; }
}

public sealed class GeminiFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("args")]
    public JsonElement Args { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}
