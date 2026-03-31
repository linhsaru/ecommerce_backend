using System;
using Domain.Enums;
using Domain.Common;
namespace Domain.Entities;


public class Payment : BaseEntity<Guid>
{
    public Guid OrderId { get; set; }
    public PaymentMethod Method { get; set; } // Phương thức thanh toán
    public PaymentStatus Status { get; set; } = PaymentStatus.unpaid; //Trạng thái thanh toán   
    public decimal Amount { get; set; } // Số tiền thanh toán
    public string? Provider { get; set; } //Cổng thanh toán (VNPAY, MOMO, ZALOPAY...)
    public string? ProviderTxnId { get; set; } // Mã giao dịch do cổng thanh toán cung cấp
    public DateTimeOffset? PaidAt { get; set; } // Thời điểm thanh toán thành công
    public string? RawPayload { get; set; } // Lưu trữ dữ liệu thô từ cổng thanh toán
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; // Thời điểm tạo bản ghi thanh toán
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Order Order { get; set; } = null!;
}
