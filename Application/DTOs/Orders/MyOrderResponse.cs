using Domain.Enums;

namespace Application.DTOs.Orders
{
    public sealed class MyOrderResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string ShipRecipient { get; set; } = string.Empty;
        public string ShipPhone { get; set; } = string.Empty;
        public string ShipLine1 { get; set; } = string.Empty;
        public string? ShipWard { get; set; }
        public string? ShipDistrict { get; set; }
        public string? ShipProvince { get; set; }
        public List<MyOrderItemResponse> Items { get; set; } = new();
    }

    public sealed class MyOrderItemResponse
    {
        public Guid ProductId { get; set; }
        public Guid VariantId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? VariantName { get; set; }
        public string? ProductImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
