namespace Domain.Enums;

/// <summary>
/// Trang thai van chuyen: pending, ready, shipped, delivered, returned, cancelled.
/// </summary>
public enum ShipmentStatus
{
    pending = 0,
    ready = 1,
    shipped = 2,
    delivered = 3,
    returned = 4,
    cancelled = 5
}
