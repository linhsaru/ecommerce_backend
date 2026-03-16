using Domain.Entities;

namespace Domain.Interfaces.Repositories
{
    public interface IUserRepository
    {
        IQueryable<User> GetQueryable();
        Task<User?> GetUserAsync(string userInfo, CancellationToken cancellationToken);
        Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default);
        Task AddAsync(User user, CancellationToken cancellationToken);
        void Update(User user);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
