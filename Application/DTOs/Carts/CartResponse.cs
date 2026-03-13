namespace Application.DTOs.Carts;


// Trả về khi thêm vào giỏ (guest: dữ liệu lưu LocalStorage; đã đăng nhập: giỏ từ DB).
public sealed class CartResponse
{
    public bool IsGuest { get; init; }
    public List<CartItemResponse> Items { get; init; } = new();
}

public sealed class CartItemResponse
{
    public Guid ProductId { get; init; }
    public Guid VariantId { get; init; }
    public int Quantity { get; init; }
    public string? ProductName { get; init; }
    public string? VariantName { get; init; }
    public decimal Price { get; init; }
}
