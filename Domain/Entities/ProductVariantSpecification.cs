using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class ProductVariantSpecification
    {
        public Guid Id { get; set; }
        public Guid ProductVariantId { get; set; }
        public Guid SpecificationTypeId { get; set; }
        public string? Value { get; set; } = null;
        public ProductVariant ProductVariant { get; set; } = null!;
        public SpecificationType SpecificationType { get; set; } = null!;
    }
}
