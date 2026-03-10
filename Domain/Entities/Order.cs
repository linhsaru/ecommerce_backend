using System;
using System.Collections.Generic;
using Domain.Enums;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Don hang: order_no, user_id, status, payment_status, subtotal/discount/shipping/total, snapshot dia chi giao hang.
/// </summary>
public class Order : BaseEntity<Guid>
{
    public required string OrderNo { get; set; }
    public Guid? UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.unpaid;
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public long? CouponId { get; set; }
    public string? Note { get; set; }

    public required string ShipRecipient { get; set; }
    public required string ShipPhone { get; set; }
    public required string ShipLine1 { get; set; }
    public string? ShipLine2 { get; set; }
    public string? ShipWard { get; set; }
    public string? ShipDistrict { get; set; }
    public string? ShipProvince { get; set; }
    public string ShipCountry { get; set; } = "VN";
    public string? ShipPostalCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public User? User { get; set; }
    public Coupon? Coupon { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
}
