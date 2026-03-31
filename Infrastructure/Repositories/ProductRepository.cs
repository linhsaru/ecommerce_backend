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
            => await _db.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductVariants)
                    .ThenInclude(v => v.ProductVariantSpecifications)
                        .ThenInclude(s => s.SpecificationType)
                .FirstOrDefaultAsync(p => p.Id.Equals(id) && p.DeletedAt == null, ct);

        public async Task<ProductVariant?> GetVariantByIdAsync(Guid variantId, CancellationToken ct = default)
            => await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId && v.DeletedAt == null, ct);

        public async Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => await _db.Products
                .Include(p => p.Brand)
                .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.ProductVariants)
                    .ThenInclude(v => v.ProductVariantSpecifications)
                        .ThenInclude(s => s.SpecificationType)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.DeletedAt == null, ct);

        public async Task<List<ProductVariant>> GetVariantsByProductIdAsync(Guid productId, CancellationToken ct = default)
            => await _db.ProductVariants
                .AsNoTracking()
                .Where(v => v.ProductId == productId && v.DeletedAt == null)
                .Include(v => v.ProductVariantSpecifications)
                    .ThenInclude(s => s.SpecificationType)
                .OrderBy(v => v.Price)
                .ThenBy(v => v.Sku)
                .ToListAsync(ct);

        public IQueryable<Product> GetQueryable()
            => _db.Products.AsNoTracking().Where(p => p.DeletedAt == null);

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);

    }
}
