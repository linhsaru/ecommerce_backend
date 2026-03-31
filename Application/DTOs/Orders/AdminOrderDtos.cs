using Domain.Enums;

namespace Application.DTOs.Orders
{
    public sealed class AdminOrderResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public OrderStatus Status { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public ShipmentStatus? ShipmentStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public string ShipRecipient { get; set; } = string.Empty;
        public string ShipPhone { get; set; } = string.Empty;
        public string ShipLine1 { get; set; } = string.Empty;
        public string? ShipWard { get; set; }
        public string? ShipDistrict { get; set; }
        public string? ShipProvince { get; set; }
        public List<MyOrderItemResponse> Items { get; set; } = new();
    }

    public sealed class UpdateOrderStatusesRequest
    {
        public OrderStatus? OrderStatus { get; set; }
        public PaymentStatus? PaymentStatus { get; set; }
        public ShipmentStatus? ShipmentStatus { get; set; }
    }
}
