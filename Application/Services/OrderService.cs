using Application.DTOs.Orders;
using Application.Common;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _repo;
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IOrderRepository repo,
            IUserRepository userRepository,
            IEmailService emailService,
            ILogger<OrderService> logger)
        {
            _repo = repo;
            _userRepository = userRepository;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<Result<CreateOrderResponse>> CreateOrderAsync(CreateOrderRequest request)
        {
            if (request.Items is null || request.Items.Count == 0)
                return Result<CreateOrderResponse>.Fail("VALIDATION_ERROR", "Đơn hàng phải có ít nhất 1 sản phẩm.");

            if (request.Items.Any(i => i.Quantity <= 0))
                return Result<CreateOrderResponse>.Fail("VALIDATION_ERROR", "Số lượng sản phẩm phải lớn hơn 0.");

            if (string.IsNullOrWhiteSpace(request.ShippingAddress) || string.IsNullOrWhiteSpace(request.PhoneNumber))
                return Result<CreateOrderResponse>.Fail("VALIDATION_ERROR", "Thiếu thông tin địa chỉ hoặc số điện thoại giao hàng.");

            if (string.IsNullOrWhiteSpace(request.RecipientEmail))
                return Result<CreateOrderResponse>.Fail("VALIDATION_ERROR", "Thiếu email người nhận hàng.");

            try
            {
                var createResult = await _repo.ExecuteInTransactionAsync(
                    async () =>
                    {
                        var productIds = request.Items.Select(i => i.ProductVariantId).ToList();
                        var variants = await _repo.GetVariantsByIdsAsync(productIds);

                        if (variants.Count != request.Items.DistinctBy(i => i.ProductVariantId).Count())
                            return Result<CreateOrderResponse>.Fail("NOT_FOUND", "Một hoặc nhiều biến thể sản phẩm không tồn tại.");

                        decimal totalAmount = 0;
                        var orderItems = new List<OrderItem>();

                        foreach (var item in request.Items)
                        {
                            var variant = variants.First(v => v.Id == item.ProductVariantId);
                            var availableStock = await _repo.GetAvailableStockAsync(item.ProductVariantId);

                            if (availableStock < item.Quantity)
                                return Result<CreateOrderResponse>.Fail("INSUFFICIENT_STOCK", $"Biến thể {variant.Sku} không đủ hàng.");

                            var total = variant.Price * item.Quantity;
                            totalAmount += total;

                            orderItems.Add(new OrderItem
                            {
                                Id = Guid.NewGuid(),
                                ProductId = variant.ProductId,
                                VariantId = variant.Id,
                                Sku = variant.Sku,
                                Name = variant.Product.Name,
                                VariantName = variant.VariantName,
                                Quantity = item.Quantity,
                                UnitPrice = variant.Price,
                                LineTotal = total,
                                CreatedAt = DateTimeOffset.UtcNow
                            });
                        }

                        var order = new Order
                        {
                            Id = Guid.NewGuid(),
                            OrderNo = $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}",
                            UserId = request.UserId,
                            Status = OrderStatus.pending,
                            PaymentStatus = PaymentStatus.unpaid,
                            SubtotalAmount = totalAmount,
                            DiscountAmount = 0,
                            ShippingAmount = 0,
                            TotalAmount = totalAmount,
                            ShipRecipient = request.RecipientName ?? "Khách hàng",
                            ShipPhone = request.PhoneNumber,
                            ShipLine1 = request.ShippingAddress,
                            ShipWard = request.Ward,
                            ShipProvince = request.Province,
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };

                        foreach (var item in orderItems)
                        {
                            item.OrderId = order.Id;
                        }

                        var payment = new Payment
                        {
                            Id = Guid.NewGuid(),
                            OrderId = order.Id,
                            Method = request.PaymentMethod,
                            Status = PaymentStatus.unpaid,
                            Amount = totalAmount,
                            CreatedAt = DateTimeOffset.UtcNow,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };

                        await _repo.AddOrderAsync(order);
                        await _repo.AddOrderItemsAsync(orderItems);
                        await _repo.AddPaymentAsync(payment);

                        foreach (var item in request.Items)
                        {
                            await _repo.DeductStockAsync(item.ProductVariantId, item.Quantity);
                        }

                        return Result<CreateOrderResponse>.Ok(new CreateOrderResponse
                        {
                            OrderId = order.Id,
                            OrderNo = order.OrderNo,
                            TotalAmount = totalAmount,
                            Status = order.Status
                        });
                    },
                    r => r.IsSuccess);

                // Send immediate confirmation only for COD.
                if (createResult.IsSuccess && request.PaymentMethod == PaymentMethod.cod)
                {
                    await TrySendOrderConfirmationEmailAsync(createResult.Value!, request.UserId, request.RecipientEmail);
                }

                return createResult;
            }
            catch (Exception ex)
            {
                return Result<CreateOrderResponse>.Fail("ORDER_CREATE_FAILED", "Tạo đơn hàng thất bại.", ex.Message);
            }
        }

        private async Task TrySendOrderConfirmationEmailAsync(CreateOrderResponse createdOrder, Guid? userId, string? recipientEmail)
        {
            try
            {
                var order = await _repo.GetOrderByIdAsync(createdOrder.OrderId);
                if (order == null || order.OrderItems.Count == 0)
                    return;

                Domain.Entities.User? user = null;
                if (userId.HasValue)
                {
                    user = await _userRepository.GetByIdAsync(userId.Value, CancellationToken.None);
                }

                var targetEmail = string.IsNullOrWhiteSpace(recipientEmail) ? user?.Email : recipientEmail.Trim();
                if (string.IsNullOrWhiteSpace(targetEmail))
                    return;

                await _emailService.SendOrderConfirmationAsync(new OrderConfirmationEmailRequest
                {
                    RecipientEmail = targetEmail,
                    RecipientName = user?.FullName != null ? user?.FullName : order.ShipRecipient,
                    ReciptientAddress = order?.ShipLine1,
                    PhoneNumber = order?.ShipPhone,
                    OrderNo = order.OrderNo,
                    OrderedAt = order.CreatedAt,
                    PaymentStatus = order.PaymentStatus,
                    TotalAmount = order.TotalAmount,
                    Items = order.OrderItems.Select(item => new OrderConfirmationEmailItem
                    {
                        ProductName = item.Name,
                        VariantName = item.VariantName,
                        Quantity = item.Quantity,
                        LineTotal = item.LineTotal
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                // Email should not block successful order creation.
                _logger.LogError(ex, "Failed to send order confirmation email for order {OrderId}", createdOrder.OrderId);
            }
        }

        public async Task<Result<List<MyOrderResponse>>> GetMyOrdersAsync(Guid userId)
        {
            try
            {
                var orders = await _repo.GetOrdersByUserIdAsync(userId);

                var variantIds = orders
                    .SelectMany(o => o.OrderItems)
                    .Select(i => i.VariantId)
                    .Distinct()
                    .ToList();
                var variants = await _repo.GetVariantsByIdsAsync(variantIds);
                var variantById = variants.ToDictionary(v => v.Id, v => v);

                var response = orders.Select(order => new MyOrderResponse
                {
                    OrderId = order.Id,
                    OrderNo = order.OrderNo,
                    Status = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    TotalAmount = order.TotalAmount,
                    CreatedAt = order.CreatedAt,
                    ShipRecipient = order.ShipRecipient,
                    ShipPhone = order.ShipPhone,
                    ShipLine1 = order.ShipLine1,
                    ShipWard = order.ShipWard,
                    ShipDistrict = order.ShipDistrict,
                    ShipProvince = order.ShipProvince,
                    Items = order.OrderItems.Select(item => new MyOrderItemResponse
                    {
                        ProductId = item.ProductId,
                        VariantId = item.VariantId,
                        Sku = item.Sku,
                        Name = item.Name,
                        VariantName = item.VariantName,
                        ProductImageUrl = variantById.TryGetValue(item.VariantId, out var v)
                            ? (v.Product.ThumbnailUrl ??
                               v.Product.ProductImages
                                   .OrderBy(pi => pi.SortOrder)
                                   .Select(pi => pi.Url)
                                   .FirstOrDefault())
                            : null,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal
                    }).ToList()
                }).ToList();

                return Result<List<MyOrderResponse>>.Ok(response);
            }
            catch (Exception ex)
            {
                return Result<List<MyOrderResponse>>.Fail("ORDER_LIST_FAILED", "Lấy danh sách đơn hàng thất bại.", ex.Message);
            }
        }

        public async Task<Result<OrderLookupResponse>> GetOrderByOrderNoAsync(string orderNo)
        {
            try
            {
                var order = await _repo.GetOrderByOrderNoAsync(orderNo);
                if (order == null)
                    return Result<OrderLookupResponse>.Fail("NOT_FOUND", "Không tìm thấy đơn hàng.");

                var variantIds = order.OrderItems
                    .Select(i => i.VariantId)
                    .Distinct()
                    .ToList();
                var variants = await _repo.GetVariantsByIdsAsync(variantIds);
                var variantById = variants.ToDictionary(v => v.Id, v => v);

                var latestShipment = order.Shipments
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefault();

                return Result<OrderLookupResponse>.Ok(new OrderLookupResponse
                {
                    OrderNo = order.OrderNo,
                    TotalAmount = order.TotalAmount,
                    PaymentStatus = order.PaymentStatus,
                    Status = order.Status,
                    ShipmentStatus = latestShipment?.Status,
                    Items = order.OrderItems.Select(item => new MyOrderItemResponse
                    {
                        ProductId = item.ProductId,
                        VariantId = item.VariantId,
                        Sku = item.Sku,
                        Name = item.Name,
                        VariantName = item.VariantName,
                        ProductImageUrl = variantById.TryGetValue(item.VariantId, out var v)
                            ? (v.Product.ThumbnailUrl ??
                               v.Product.ProductImages
                                   .OrderBy(pi => pi.SortOrder)
                                   .Select(pi => pi.Url)
                                   .FirstOrDefault())
                            : null,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return Result<OrderLookupResponse>.Fail("ORDER_LOOKUP_FAILED", "Tra cứu đơn hàng thất bại.", ex.Message);
            }
        }

        public async Task<Result<(List<AdminOrderResponse> Items, long Total)>> GetOrdersPagedAsync(
            int page,
            int pageSize,
            string? search,
            int? status,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var (orders, total) = await _repo.GetOrdersPagedAsync(page, pageSize, search, status, cancellationToken);
                var response = orders.Select(MapAdminOrder).ToList();
                return Result<(List<AdminOrderResponse> Items, long Total)>.Ok((response, total));
            }
            catch (Exception ex)
            {
                return Result<(List<AdminOrderResponse> Items, long Total)>.Fail("ORDER_LIST_FAILED", "Lấy danh sách đơn hàng thất bại.", ex.Message);
            }
        }

        public async Task<Result<AdminOrderResponse>> UpdateOrderStatusesAsync(Guid orderId, UpdateOrderStatusesRequest request)
        {
            if (!request.OrderStatus.HasValue && !request.PaymentStatus.HasValue && !request.ShipmentStatus.HasValue)
                return Result<AdminOrderResponse>.Fail("VALIDATION_ERROR", "Cần ít nhất 1 trạng thái để cập nhật.");

            try
            {
                var order = await _repo.GetOrderByIdAsync(orderId);
                if (order == null)
                    return Result<AdminOrderResponse>.Fail("NOT_FOUND", "Không tìm thấy đơn hàng.");

                var now = DateTimeOffset.UtcNow;

                if (request.OrderStatus.HasValue)
                {
                    order.Status = request.OrderStatus.Value;
                    if (request.OrderStatus.Value == OrderStatus.cancelled)
                        order.CancelledAt = now;
                    if (request.OrderStatus.Value == OrderStatus.completed)
                        order.CompletedAt = now;
                }

                if (request.PaymentStatus.HasValue)
                    order.PaymentStatus = request.PaymentStatus.Value;

                if (request.ShipmentStatus.HasValue)
                {
                    var shipment = await _repo.GetLatestShipmentByOrderIdAsync(orderId);
                    if (shipment == null)
                    {
                        shipment = new Shipment
                        {
                            Id = Guid.NewGuid(),
                            OrderId = orderId,
                            Status = request.ShipmentStatus.Value,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        await _repo.AddShipmentAsync(shipment);
                    }
                    else
                    {
                        shipment.Status = request.ShipmentStatus.Value;
                        shipment.UpdatedAt = now;
                    }
                }

                order.UpdatedAt = now;
                await _repo.SaveChangesAsync();

                var updated = await _repo.GetOrderByIdAsync(orderId);
                if (updated == null)
                    return Result<AdminOrderResponse>.Fail("NOT_FOUND", "Không tìm thấy đơn hàng.");

                return Result<AdminOrderResponse>.Ok(MapAdminOrder(updated));
            }
            catch (Exception ex)
            {
                return Result<AdminOrderResponse>.Fail("ORDER_UPDATE_FAILED", "Cập nhật trạng thái đơn hàng thất bại.", ex.Message);
            }
        }

        private static AdminOrderResponse MapAdminOrder(Order order)
        {
            var latestShipment = order.Shipments
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefault();

            return new AdminOrderResponse
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                UserId = order.UserId,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                ShipmentStatus = latestShipment?.Status,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt,
                ShipRecipient = order.ShipRecipient,
                ShipPhone = order.ShipPhone,
                ShipLine1 = order.ShipLine1,
                ShipWard = order.ShipWard,
                ShipDistrict = order.ShipDistrict,
                ShipProvince = order.ShipProvince,
                Items = order.OrderItems.Select(item => new MyOrderItemResponse
                {
                    ProductId = item.ProductId,
                    VariantId = item.VariantId,
                    Sku = item.Sku,
                    Name = item.Name,
                    VariantName = item.VariantName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineTotal = item.LineTotal
                }).ToList()
            };
        }
    }
}
