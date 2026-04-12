using Domain.Enums;

namespace Application.DTOs.Orders
{
    public sealed class OrderConfirmationEmailRequest
    {
        public string RecipientEmail { get; set; } = null!;
        public string? RecipientName { get; set; }
        public string OrderNo { get; set; } = null!;
        public DateTimeOffset OrderedAt { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderConfirmationEmailItem> Items { get; set; } = new();
    }

    public sealed class OrderConfirmationEmailItem
    {
        public string ProductName { get; set; } = null!;
        public string? VariantName { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
    }
}
