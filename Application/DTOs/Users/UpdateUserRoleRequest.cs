using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Users;

public sealed class UpdateUserRoleRequest
{
    [Required]
    public Guid RoleId { get; set; }
}
