using System;
using System.Collections.Generic;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Kho hang: name, code.
/// </summary>
public class Warehouse : BaseEntity<long>
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
}
