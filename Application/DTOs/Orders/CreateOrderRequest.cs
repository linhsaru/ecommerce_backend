using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Orders
{
    public sealed class CreateOrderRequest
    {
        public Guid? UserId { get; set; }
        public string RecipientEmail { get; set; } = null!;
        public string? RecipientName { get; set; }
        public string ShippingAddress { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string? Ward { get; set; }
        public string? Province { get; set; }

        public PaymentMethod PaymentMethod { get; set; } 

        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public sealed class OrderItemRequest
    {
        public Guid ProductVariantId { get; set; }
        public int Quantity { get; set; }
    }

}
