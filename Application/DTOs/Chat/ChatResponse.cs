namespace Application.DTOs.Chat;

public sealed class ChatResponse
{
    public string Reply { get; set; } = "";

    public string? ModelUsed { get; set; }

    public string? SessionId { get; set; }

    public int? TotalMatchedProducts { get; set; }

    public int? ReturnedProducts { get; set; }

    public bool? HasMoreProducts { get; set; }
}

