using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class CartRepository : ICartRepository
    {
        public readonly IAppDbContext _db;

        public CartRepository(IAppDbContext context) => _db = context;

        public async Task CreateCartAsync(Cart cart)
        {
            await _db.Carts.AddAsync(cart);
        }

        public async Task<Cart?> GetCartUserByUserId(Guid userId)
        {
            return await _db.Carts
                .Include(c => c.CartItems!)
                .ThenInclude(ci => ci.Variant!)
                .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public void RemoveCartItem(CartItem item)
        {
            _db.CartItems.Remove(item);
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }
    }
}
