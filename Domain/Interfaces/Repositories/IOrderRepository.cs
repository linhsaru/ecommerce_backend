using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Interfaces.Repositories
{
    public interface IOrderRepository
    {
        Task<List<ProductVariant>> GetVariantsByIdsAsync(List<Guid> ids);
        Task<List<Order>> GetOrdersByUserIdAsync(Guid userId);
        Task<(List<Order> Items, long Total)> GetOrdersPagedAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default);
        Task<Order?> GetOrderByIdAsync(Guid orderId);
        Task<Order?> GetOrderByOrderNoAsync(string orderNo);
        Task<Shipment?> GetLatestShipmentByOrderIdAsync(Guid orderId);
        Task AddShipmentAsync(Shipment shipment);
        Task<int> GetAvailableStockAsync(Guid variantId);
        Task DeductStockAsync(Guid variantId, int quantity);
        Task AddOrderAsync(Order order);
        Task AddOrderItemsAsync(List<OrderItem> items);
        Task AddPaymentAsync(Payment payment);
        Task SaveChangesAsync();
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, Func<T, bool> shouldCommit);
    }
}
