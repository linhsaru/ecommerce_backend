using API.Common;
using API.Contracts;
using Application.DTOs.Orders;
using Application.Interfaces.Services;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController : BaseApiController
{
    private readonly IOrderService _orderService;
    private readonly IUserRepository _userRepository;

    public OrdersController(IOrderService orderService, IUserRepository userRepository)
    {
        _orderService = orderService;
        _userRepository = userRepository;
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CreateOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken);
        request.UserId = userId;
        var result = await _orderService.CreateOrderAsync(request);
        return result.ToActionResult(this);
    }

    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<MyOrderResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyOrders(CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken);
        if (userId == null)
            return Unauthorized();

        var result = await _orderService.GetMyOrdersAsync(userId.Value);
        return result.ToActionResult(this);
    }

    [HttpGet("lookup/{orderNo}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<OrderLookupResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LookupByOrderNo(string orderNo, CancellationToken cancellationToken = default)
    {
        var result = await _orderService.GetOrderByOrderNoAsync(orderNo);
        return result.ToActionResult(this);
    }

    [HttpGet]
    [Authorize(Roles = nameof(UserRole.RoleAdmin))]
    [ProducesResponseType(typeof(ApiResponse<List<AdminOrderResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllOrders()
    {
        var result = await _orderService.GetAllOrdersAsync();
        return result.ToActionResult(this);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = nameof(UserRole.RoleAdmin))]
    [ProducesResponseType(typeof(ApiResponse<AdminOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatuses(
        Guid id,
        [FromBody] UpdateOrderStatusesRequest request)
    {
        var result = await _orderService.UpdateOrderStatusesAsync(id, request);
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
