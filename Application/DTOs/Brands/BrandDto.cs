using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Brands
{
    public sealed class BrandDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public string Slug { get; init; } = "";
    }
}
