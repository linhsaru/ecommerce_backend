using Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class PromotionProduct : BaseEntity<Guid>
    {
        public Guid PromotionId { get; set; }

        public Guid ProductId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountPrice { get; set; }

        // Navigation
        public Promotion? Promotion { get; set; }

        public Product? Product { get; set; }
    }
}
