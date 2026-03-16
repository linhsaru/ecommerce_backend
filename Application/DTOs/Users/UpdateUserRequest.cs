using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Users;

/// <summary>
/// Request cập nhật user (các field null = không đổi).
/// </summary>
public sealed class UpdateUserRequest
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string? Username { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MinLength(6)]
    public string? Password { get; set; }

    public Guid? RoleId { get; set; }
    public int? Status { get; set; }
}
