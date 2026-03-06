using System.Collections.Generic;
using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Thuong hieu san pham: name, slug.
/// </summary>
public class Brand : SoftDeleteEntity<long>
{
    public required string Name { get; set; }
    public required string Slug { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
