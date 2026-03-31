using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class RoleRepository : IRoleRepository
    {
        private readonly IAppDbContext _db;
        public RoleRepository(IAppDbContext db) => _db = db;

        public async Task<Role> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        {
            return await _db.Roles.FindAsync(new object[] { roleId }, cancellationToken) 
                ?? throw new KeyNotFoundException($"Role with ID {roleId} not found.");
        }
    }
}
