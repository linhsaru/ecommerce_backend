namespace Application.DTOs.Carts;

public sealed class UpdateCartItemQuantityDto
{
    // delta > 0: tang so luong, delta < 0: giam so luong.
    public int Delta { get; set; }
}
