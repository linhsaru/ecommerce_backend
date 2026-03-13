using API.Common;
using API.Contracts;
using Application.Common;
using Application.DTOs.Carts;
using Application.Interfaces.Services;
using Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

// Giỏ hàng: không bắt buộc đăng nhập. Khách → frontend lưu LocalStorage; đã đăng nhập → lưu DB, kiểm tra tồn kho.
[ApiController]
[Route("cart")]
public class CartController : BaseApiController
{
    private readonly ICartService _cartService;
    private readonly IUserRepository _userRepository;

    public CartController(ICartService cartService, IUserRepository userRepository)
    {
        _cartService = cartService;
        _userRepository = userRepository;
    }

    // Thêm sản phẩm vào giỏ. Không auth: trả về dữ liệu lưu LocalStorage. Có auth: kiểm tra tồn kho và lưu DB.
    [HttpPost("items")]
    [ProducesResponseType(typeof(ApiResponse<CartResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartDto dto, CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken);
        var result = await _cartService.AddToCartAsync(userId, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    // Xem giỏ hàng. Không auth: trả giỏ rỗng (frontend đọc từ LocalStorage). Có auth: trả giỏ từ DB.
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CartResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken);
        var result = await _cartService.GetCartAsync(userId, cancellationToken);
        return result.ToActionResult(this);
    }

    private async Task<Guid?> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(User.Identity.Name))
            return null;
        var user = await _userRepository.GetUserAsync(User.Identity.Name, cancellationToken);
        return user?.Id;
    }
}
