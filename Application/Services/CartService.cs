using Application.Common;
using Application.DTOs.Carts;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Services
{
    public sealed class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly IProductRepository _productRepository;
        private readonly IInventoryRepository _inventoryRepository;

        public CartService(ICartRepository cartRepository, IProductRepository productRepository, IInventoryRepository inventoryRepository)
        {
            _cartRepository = cartRepository;
            _productRepository = productRepository;
            _inventoryRepository = inventoryRepository;
        }

        public async Task<Result<CartResponse>> AddToCartAsync(Guid? userId, AddToCartDto dto, CancellationToken cancellationToken = default)
        {
            var variant = await _productRepository.GetVariantByIdAsync(dto.VariantId, cancellationToken);
            if (variant == null)
                return Result<CartResponse>.Fail("VariantNotFound", "Biến thể sản phẩm không tồn tại");
            if (variant.ProductId != dto.ProductId)
                return Result<CartResponse>.Fail("InvalidVariant", "Biến thể không thuộc sản phẩm này");

            if (dto.Quantity <= 0)
                return Result<CartResponse>.Fail("InvalidQuantity", "Số lượng phải lớn hơn 0");

            // Khách chưa đăng nhập: trả về dữ liệu để frontend lưu LocalStorage
            if (userId == null)
            {
                var product = await _productRepository.GetByIdAsync(dto.ProductId, cancellationToken);
                var item = new CartItemResponse
                {
                    ProductId = dto.ProductId,
                    VariantId = dto.VariantId,
                    Quantity = dto.Quantity,
                    ProductName = product?.Name,
                    VariantName = variant.VariantName,
                    Price = variant.Price
                };
                return Result<CartResponse>.Ok(new CartResponse
                {
                    IsGuest = true,
                    Items = new List<CartItemResponse> { item }
                });
            }

            // Đã đăng nhập: kiểm tra tồn kho rồi lưu DB
            var totalStock = await _inventoryRepository.GetTotalStockAsync(dto.VariantId, cancellationToken);
            if (totalStock < dto.Quantity)
                return Result<CartResponse>.Fail("InsufficientStock", "Số lượng sản phẩm trong kho không đủ");

            var cart = await _cartRepository.GetCartUserByUserId(userId.Value);
            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                await _cartRepository.CreateCartAsync(cart);
                await _cartRepository.SaveChangesAsync();
            }

            var existingItem = cart.CartItems?.FirstOrDefault(ci => ci.VariantId == dto.VariantId);
            if (existingItem != null)
            {
                var newQty = existingItem.Quantity + dto.Quantity;
                if (totalStock < newQty)
                    return Result<CartResponse>.Fail("InsufficientStock", "Tổng số lượng trong giỏ vượt quá tồn kho");
                existingItem.Quantity = newQty;
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    VariantId = dto.VariantId,
                    Quantity = dto.Quantity
                };
                if (cart.CartItems == null)
                    cart.CartItems = new List<CartItem>();
                cart.CartItems.Add(cartItem);
            }

            await _cartRepository.SaveChangesAsync();

            // Load lại cart có Include để trả response
            cart = await _cartRepository.GetCartUserByUserId(userId.Value);
            var response = MapCartToResponse(cart!, isGuest: false);
            return Result<CartResponse>.Ok(response);
        }

        public async Task<Result<CartResponse>> GetCartAsync(Guid? userId, CancellationToken cancellationToken = default)
        {
            if (userId == null)
                return Result<CartResponse>.Ok(new CartResponse { IsGuest = true, Items = new List<CartItemResponse>() });

            var cart = await _cartRepository.GetCartUserByUserId(userId.Value);
            var response = MapCartToResponse(cart, isGuest: false);
            return Result<CartResponse>.Ok(response);
        }

        private static CartResponse MapCartToResponse(Cart? cart, bool isGuest)
        {
            if (cart?.CartItems == null || !cart.CartItems.Any())
                return new CartResponse { IsGuest = isGuest, Items = new List<CartItemResponse>() };

            var items = cart.CartItems.Select(ci => new CartItemResponse
            {
                ProductId = ci.Variant?.ProductId ?? Guid.Empty,
                VariantId = ci.VariantId,
                Quantity = ci.Quantity,
                ProductName = ci.Variant?.Product?.Name,
                VariantName = ci.Variant?.VariantName,
                Price = ci.Variant?.Price ?? 0
            }).ToList();

            return new CartResponse { IsGuest = isGuest, Items = items };
        }
    }
}
