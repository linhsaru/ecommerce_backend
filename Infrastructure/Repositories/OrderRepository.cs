using Domain.Entities;
using Domain.Interfaces.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;

        public OrderRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductVariant>> GetVariantsByIdsAsync(List<Guid> ids)
        {
            return await _context.ProductVariants
                .Include(v => v.Product)
                    .ThenInclude(p => p.ProductImages)
                .Where(v => ids.Contains(v.Id))
                .ToListAsync();
        }

        public async Task<List<Order>> GetOrdersByUserIdAsync(Guid userId)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Order>> GetAllOrdersAsync()
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Shipments)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<Order?> GetOrderByIdAsync(Guid orderId)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Shipments)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<Order?> GetOrderByOrderNoAsync(string orderNo)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.Shipments)
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
        }

        public async Task<Shipment?> GetLatestShipmentByOrderIdAsync(Guid orderId)
        {
            return await _context.Shipments
                .Where(s => s.OrderId == orderId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetAvailableStockAsync(Guid variantId)
        {
            var inventories = await _context.Inventories
                .Where(i => i.VariantId == variantId)
                .ToListAsync();

            return inventories.Sum(i => i.Quantity - i.Reserved);
        }

        public async Task DeductStockAsync(Guid variantId, int quantity)
        {
            var inventories = await _context.Inventories
                .Where(i => i.VariantId == variantId && i.Quantity > 0)
                .OrderByDescending(i => i.Quantity)
                .ToListAsync();

            var remaining = quantity;
            foreach (var inventory in inventories)
            {
                if (remaining <= 0)
                    break;

                var deduct = Math.Min(inventory.Quantity, remaining);
                inventory.Quantity -= deduct;
                remaining -= deduct;
            }

            if (remaining > 0)
                throw new InvalidOperationException("Không đủ tồn kho để trừ.");
        }

        public async Task AddOrderAsync(Order order)
        {
            await _context.Orders.AddAsync(order);
        }

        public async Task AddOrderItemsAsync(List<OrderItem> items)
        {
            await _context.OrderItems.AddRangeAsync(items);
        }

        public async Task AddPaymentAsync(Payment payment)
        {
            await _context.Payments.AddAsync(payment);
        }

        public async Task AddShipmentAsync(Shipment shipment)
        {
            await _context.Shipments.AddAsync(shipment);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, Func<T, bool> shouldCommit)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var result = await action();
                if (!shouldCommit(result))
                {
                    await transaction.RollbackAsync();
                    return result;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

    }
}
