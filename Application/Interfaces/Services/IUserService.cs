using Application.Common;
using Application.DTOs.Users;

namespace Application.Interfaces.Services;

public interface IUserService
{
    Task<Result<(List<UserDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default);
    Task<Result<UserDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> UpdateUserRoleAsync(Guid id, Guid roleId, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> RemoveUserRoleAsync(Guid id, CancellationToken cancellationToken = default);
}
