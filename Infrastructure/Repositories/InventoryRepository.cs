using Application.Interfaces;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class InventoryRepository : IInventoryRepository
    {
        private readonly IAppDbContext _db;

        public InventoryRepository(IAppDbContext db) => _db = db;

        public async Task<int> GetTotalStockAsync(Guid variantId, CancellationToken ct = default)
        {
            return await _db.Inventories
                .Where(i => i.VariantId == variantId).SumAsync(i => i.Quantity, ct);
        }
    }
}
