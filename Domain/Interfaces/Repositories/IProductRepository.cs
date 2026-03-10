using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces.Repositories
{
    public interface IProductRepository
    {
        IQueryable<Product> GetQueryable();
        Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default);
        Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken ct = default);
        Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default);
        void Add(Product product);
        void Update(Product product);
        void AddProductCategories(IEnumerable<ProductCategory> productCategories);
        void RemoveProductCategories(IEnumerable<ProductCategory> productCategories);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
