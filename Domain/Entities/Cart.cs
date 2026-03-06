using System;
using System.Collections.Generic;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Gio hang: user_id hoac session_id (khach vang lai).
/// </summary>
public class Cart : BaseEntity<long>
{
    public long? UserId { get; set; }
    public string? SessionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
}
