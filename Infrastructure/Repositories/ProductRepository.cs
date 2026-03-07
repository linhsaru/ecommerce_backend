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
    public class ProductRepository : IProductRepository
    {

        public readonly IAppDbContext _db;
        public ProductRepository(IAppDbContext db)
        {
            _db = db;
        }
        public void Add(Product product) => _db.Products.Add(product);
        public void Update(Product product)
            => _db.Products.Update(product);
        public void AddProductCategories(IEnumerable<ProductCategory> productCategories)
        {
            _db.ProductCategories.AddRange(productCategories);
        }

        public void RemoveProductCategories(IEnumerable<ProductCategory> productCategories)
        {
            _db.ProductCategories.RemoveRange(productCategories);
        }

        public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.Products.AnyAsync(p => p.Id.Equals(id) && p.DeletedAt == null, ct);

        public async Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken ct = default)
            => await _db.Products.AnyAsync(p => p.Slug == slug && !p.Id.Equals(excludeId) && p.DeletedAt == null, ct);

        public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.Products.FirstOrDefaultAsync(p => p.Id.Equals(id) && p.DeletedAt == null, ct);

        public async Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => await _db.Products.FirstOrDefaultAsync(p => p.Slug == slug && p.DeletedAt == null, ct);

        public IQueryable<Product> GetQueryable()
            => _db.Products.AsNoTracking().Where(p => p.DeletedAt == null);

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);

    }
}
