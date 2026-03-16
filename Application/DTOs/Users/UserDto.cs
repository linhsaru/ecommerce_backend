namespace Application.DTOs.Users;

/// <summary>
/// DTO thông tin user (không bao gồm mật khẩu).
/// </summary>
public sealed class UserDto
{
    public Guid Id { get; init; }
    public Guid? RoleId { get; init; }
    public string? RoleName { get; init; }
    public string Email { get; init; } = "";
    public string? Phone { get; init; }
    public string? FullName { get; init; }
    public string? Username { get; init; }
    public string? AvatarUrl { get; init; }
    public int Status { get; init; }
    public DateTime? LastLogin { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
