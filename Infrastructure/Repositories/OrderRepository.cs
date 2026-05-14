using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        public async Task<(List<Order> Items, long Total)> GetOrdersPagedAsync(
            int page,
            int pageSize,
            string? search,
            int? status,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .Include(o => o.Shipments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(o =>
                    o.OrderNo.Contains(s) ||
                    o.ShipRecipient.Contains(s) ||
                    o.ShipPhone.Contains(s));
            }

            if (status.HasValue && Enum.IsDefined(typeof(OrderStatus), status.Value))
                query = query.Where(o => o.Status == (OrderStatus)status.Value);

            query = query.OrderByDescending(o => o.CreatedAt);

            var total = await query.LongCountAsync(cancellationToken);
            var skip = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100);
            var items = await query
                .Skip((int)skip)
                .Take(Math.Clamp(pageSize, 1, 100))
                .ToListAsync(cancellationToken);

            return (items, total);
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
