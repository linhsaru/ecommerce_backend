using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IAppDbContext _context;

        public UserRepository(IAppDbContext context) => _context = context;

        public IQueryable<User> GetQueryable()
            => _context.Users.AsNoTracking().Where(u => u.DeletedAt == null);

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Addresses)
                .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, cancellationToken);

        public async Task<User?> GetUserAsync(string userInfo, CancellationToken cancellationToken)
            => await _context.Users
                .FirstOrDefaultAsync(u => (u.Email == userInfo || u.Username == userInfo) && u.DeletedAt == null, cancellationToken);

        public async Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => await _context.Users
                .AnyAsync(u => u.Email == email && u.DeletedAt == null && (excludeId == null || u.Id != excludeId.Value), cancellationToken);

        public async Task AddAsync(User user, CancellationToken cancellationToken)
        {
            await _context.Users.AddAsync(user, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public void Update(User user) => _context.Users.Update(user);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);
    }
}

