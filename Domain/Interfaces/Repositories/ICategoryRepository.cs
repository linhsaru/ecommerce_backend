using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces.Repositories
{
    public interface ICategoryRepository
    {
        IQueryable<Category> GetQueryable();
        Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);
        Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken ct = default);
        Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default);
        void Add(Category category);
        void Update(Category category);

        Task<int> SaveChangesAsync(CancellationToken ct = default);

    }
}
