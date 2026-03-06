using Domain.Entities;
using Domain.Interfaces.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {

        private readonly AppDbContext _context;
        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id.Equals(id), cancellationToken);
        }

        public async Task<User?> GetUserAsync(string userInfo, CancellationToken cancellationToken)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email == userInfo || u.FullName == userInfo, cancellationToken);
        }

        public async Task AddAsync(User user, CancellationToken cancellationToken)
        {
            await _context.Users.AddAsync(user, cancellationToken);

            // Lưu thay đổi vào Database
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);

            // Lưu thay đổi
            await _context.SaveChangesAsync();
        }
    }
}

