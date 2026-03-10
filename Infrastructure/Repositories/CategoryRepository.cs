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
    public class CategoryRepository : ICategoryRepository
    {
        public readonly IAppDbContext _db;

        public CategoryRepository(IAppDbContext context)
        {
            _db = context;
        }
        public IQueryable<Category> GetQueryable()
        => _db.Categories.AsNoTracking().Where(c => c.DeletedAt == null);

        public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Categories.FirstOrDefaultAsync(c => c.Id.Equals(id) && c.DeletedAt == null, ct);

        public async Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => await _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug && c.DeletedAt == null, ct);

        public async Task<bool> ExistsBySlugAsync(string slug, Guid? excludeId = null, CancellationToken ct = default)
            => await _db.Categories.AnyAsync(c => c.Slug == slug && !c.Id.Equals(excludeId) && c.DeletedAt == null, ct);

        public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default)
            => await _db.Categories.AnyAsync(c => c.Id.Equals(id) && c.DeletedAt == null, ct);

        public void Add(Category category) => _db.Categories.Add(category);

        public void Update(Category category) => _db.Categories.Update(category);

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync();
    }
}
