using Domain.Enums;
using System.Collections.Generic;

namespace Application.DTOs.Orders
{
    public sealed class OrderLookupResponse
    {
        public string OrderNo { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public OrderStatus Status { get; set; }
        public ShipmentStatus? ShipmentStatus { get; set; }
        public List<MyOrderItemResponse> Items { get; set; } = new();
    }
}

