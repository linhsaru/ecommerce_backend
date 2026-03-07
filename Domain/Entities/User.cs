using System;
using System.Collections.Generic;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Nguoi dung: email, phone, password_hash, full_name, status (1 active, 0 inactive, -1 banned).
/// </summary>
public class User : SoftDeleteEntity<Guid>
{
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string? FullName { get; set; }

    public string? Username { get; set; }
    public string? AvatarUrl { get; set; }
    /// <summary>1 active, 0 inactive, -1 banned</summary>
    public int Status { get; set; } = 1;

    public DateTime? LastLogin { get; set; }

    public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
}
