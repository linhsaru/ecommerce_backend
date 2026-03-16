using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Users;

/// <summary>
/// Request tạo user (admin).
/// </summary>
public sealed class CreateUserRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(6)]
    public string Password { get; set; } = "";

    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string? Username { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public Guid? RoleId { get; set; }
    public int Status { get; set; } = 1;
}
