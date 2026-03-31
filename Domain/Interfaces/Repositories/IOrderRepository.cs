using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces.Repositories
{
    public interface IOrderRepository
    {
        Task<List<ProductVariant>> GetVariantsByIdsAsync(List<Guid> ids);
        Task<List<Order>> GetOrdersByUserIdAsync(Guid userId);
        Task<List<Order>> GetAllOrdersAsync();
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
