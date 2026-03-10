using System;
using Domain.Enums;
using Domain.Common;
namespace Domain.Entities;

/// <summary>
/// Thanh toan: order_id, method, status, amount, provider, provider_txn_id, paid_at, raw_payload.
/// </summary>
public class Payment : BaseEntity<Guid>
{
    public Guid OrderId { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.unpaid;
    public decimal Amount { get; set; }
    public string? Provider { get; set; }
    public string? ProviderTxnId { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? RawPayload { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Order Order { get; set; } = null!;
}
