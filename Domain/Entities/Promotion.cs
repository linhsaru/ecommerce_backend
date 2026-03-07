using Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class Promotion : BaseEntity<Guid>
    {
        [MaxLength(255)]
        public required string Title { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? BannerImage { get; set; }

        [MaxLength(50)]
        public string? DiscountType { get; set; } // percent | fixed

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        public DateTimeOffset? StartDate { get; set; }

        public DateTimeOffset? EndDate { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        // Navigation
        public ICollection<PromotionProduct>? PromotionProducts { get; set; }
    }
}
