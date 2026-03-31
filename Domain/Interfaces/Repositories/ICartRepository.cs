using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces.Repositories
{
    public interface ICartRepository
    {
        Task<Cart?> GetCartUserByUserId(Guid userId);
        Task CreateCartAsync(Cart cart);
        void RemoveCartItem(CartItem item);
        Task SaveChangesAsync();
    }
}
