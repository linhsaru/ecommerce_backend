namespace Domain.Enums;

/// <summary>
/// Phuong thuc thanh toan: cod, bank_transfer, vnpay, momo, stripe, paypal, other.
/// </summary>
public enum PaymentMethod
{
    cod = 0,
    bank_transfer = 1,
    vnpay = 2,
    momo = 3,
    stripe = 4,
    paypal = 5,
    other = 6
}
