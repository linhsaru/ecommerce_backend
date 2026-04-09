using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Chat;

public sealed class ChatRequest
{
    [Required]
    public string Message { get; set; } = "";

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(100)]
    public string? SessionId { get; set; }

    public int? PageSize { get; set; }
}

