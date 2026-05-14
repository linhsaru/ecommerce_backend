using Application.DTOs.Orders;
using Application.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Services
{
    public interface IOrderService
    {
        Task<Result<CreateOrderResponse>> CreateOrderAsync(CreateOrderRequest request);
        Task<Result<List<MyOrderResponse>>> GetMyOrdersAsync(Guid userId);
        Task<Result<(List<AdminOrderResponse> Items, long Total)>> GetOrdersPagedAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default);
        Task<Result<AdminOrderResponse>> UpdateOrderStatusesAsync(Guid orderId, UpdateOrderStatusesRequest request);
        Task<Result<OrderLookupResponse>> GetOrderByOrderNoAsync(string orderNo);
    }
}
