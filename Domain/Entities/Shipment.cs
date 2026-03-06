using System;
using Domain.Enums;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Van chuyen: order_id, status, carrier, tracking_no, shipped_at, delivered_at, shipping_fee, raw_payload.
/// </summary>
public class Shipment : BaseEntity<long>
{
    public long OrderId { get; set; }
    public ShipmentStatus Status { get; set; } = ShipmentStatus.pending;
    public string? Carrier { get; set; }
    public string? TrackingNo { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public decimal ShippingFee { get; set; }
    public string? RawPayload { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Order Order { get; set; } = null!;
}
