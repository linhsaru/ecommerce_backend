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
    public class BrandRepository : IBrandRepository
    {
        private readonly IAppDbContext _db;

        public BrandRepository(IAppDbContext db) => _db = db;

        public async Task<IEnumerable<Brand>> GetAllAsync()
        {
            return await _db.Brands
                .AsNoTracking()
                .Where(b => b.DeletedAt == null)
                .OrderBy(b => b.Name)
                .ToListAsync();
        }

        public Task<Brand?> GetBySlugAsync(string slug)
        {
            return _db.Brands
                .FirstOrDefaultAsync(b => b.Slug == slug && b.DeletedAt == null);
        }
    }
}
